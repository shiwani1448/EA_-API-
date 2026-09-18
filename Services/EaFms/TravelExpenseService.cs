using System.Globalization;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public class TravelExpenseService(EaFmsDbContext db, ICurrentUserService user, IAuditService audit,
    ITravelDocumentService documents) : ITravelExpenseService
{
    private string Actor => user.UserName ?? user.UserId.ToString(CultureInfo.InvariantCulture);
    private static string? NormalizeCurrency(string? currency) => string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant();

    public async Task<TravelExpenseResponseDto> CreateAsync(long travelRequestId, SaveTravelExpenseDto dto, CancellationToken ct = default)
    {
        Validate(dto);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var parent = await ParentAsync(travelRequestId, true, ct);
        EnsureReady(parent);
        await ValidateReceiptAsync(parent.Id, dto.ReceiptAttachmentId, ct);
        var row = new TravelExpense { TravelRequestId = parent.Id, CreatedBy = Actor, CreatedDate = Clock.UtcNowTz };
        Apply(row, dto);
        db.TravelExpenses.Add(row);
        await db.SaveChangesAsync(ct);
        AddAudit("CREATE", row, parent, null);
        await db.SaveChangesAsync(ct);
        var result = await WithReceiptAsync(row, parent, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<List<TravelExpenseResponseDto>> ListAsync(long travelRequestId, CancellationToken ct = default)
    {
        var parent = await ParentAsync(travelRequestId, false, ct);
        var rows = await db.TravelExpenses.AsNoTracking().Where(x => x.TravelRequestId == parent.Id && !x.IsDeleted)
            .OrderByDescending(x => x.ExpenseDate ?? x.CreatedDate).ThenByDescending(x => x.Id).ToListAsync(ct);
        var receipts = rows.Any(x => x.ReceiptAttachmentId.HasValue)
            ? (await documents.ListAsync(parent.Id, ct)).ToDictionary(x => x.Id) : new Dictionary<long, TravelDocumentResponseDto>();
        return rows.Select(x =>
        {
            var dto = ToDto(x, parent.ReferenceNo);
            dto.Receipt = x.ReceiptAttachmentId.HasValue ? receipts.GetValueOrDefault(x.ReceiptAttachmentId.Value) : null;
            return dto;
        }).ToList();
    }

    public async Task<TravelExpenseResponseDto> GetAsync(long expenseId, CancellationToken ct = default)
    {
        var row = await db.TravelExpenses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == expenseId && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Travel expense not found.");
        return await WithReceiptAsync(row, await ParentAsync(row.TravelRequestId, false, ct), ct);
    }

    public async Task<TravelExpenseResponseDto> UpdateAsync(long expenseId, SaveTravelExpenseDto dto, CancellationToken ct = default)
    {
        Validate(dto);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (row, parent) = await LockExpenseAsync(expenseId, ct);
        EnsureReady(parent);
        if (row.Status is not ("Draft" or "Rejected")) throw new BusinessRuleException("Only Draft or Rejected expenses may be edited.");
        await ValidateReceiptAsync(parent.Id, dto.ReceiptAttachmentId, ct);
        var previous = ToDto(row, parent.ReferenceNo);
        Apply(row, dto);
        Touch(row);
        AddAudit("UPDATE", row, parent, previous);
        await db.SaveChangesAsync(ct);
        var result = await WithReceiptAsync(row, parent, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public Task<TravelExpenseResponseDto> SubmitAsync(long expenseId, CancellationToken ct = default) =>
        TransitionAsync(expenseId, "Submitted", null, ct);
    public Task<TravelExpenseResponseDto> ApproveAsync(long expenseId, CancellationToken ct = default) =>
        TransitionAsync(expenseId, "Approved", null, ct);
    public Task<TravelExpenseResponseDto> RejectAsync(long expenseId, RejectTravelExpenseDto? dto, CancellationToken ct = default) =>
        TransitionAsync(expenseId, "Rejected", dto?.RejectionReason, ct);

    private async Task<TravelExpenseResponseDto> TransitionAsync(long id, string next, string? reason, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (row, parent) = await LockExpenseAsync(id, ct);
        EnsureReady(parent);
        if (next == "Submitted" ? row.Status is not ("Draft" or "Rejected") : row.Status != "Submitted")
            throw new BusinessRuleException("Expense status transition is not allowed.");
        if (next == "Submitted") await ValidateReceiptAsync(parent.Id, row.ReceiptAttachmentId, ct);
        var previous = ToDto(row, parent.ReferenceNo);
        var now = Clock.UtcNowTz;
        row.Status = next;
        if (next == "Submitted")
        {
            row.SubmittedBy = Actor; row.SubmittedAt = now;
            row.RejectedBy = null; row.RejectedAt = null; row.RejectionReason = null;
            row.ApprovedBy = null; row.ApprovedAt = null;
        }
        else if (next == "Approved") { row.ApprovedBy = Actor; row.ApprovedAt = now; }
        else { row.RejectedBy = Actor; row.RejectedAt = now; row.RejectionReason = reason; }
        Touch(row);
        AddAudit(next == "Submitted" ? "SUBMIT" : next == "Approved" ? "APPROVE" : "REJECT", row, parent, previous);
        await db.SaveChangesAsync(ct);
        var result = await WithReceiptAsync(row, parent, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<TravelExpenseSummaryDto> SummaryAsync(long travelRequestId, CancellationToken ct = default)
    {
        var parent = await ParentAsync(travelRequestId, false, ct);
        // Only ledger records participate. Group in SQL first; normalize currency labels
        // in memory so null/blank and casing conventions also work for historical rows.
        var sums = await db.TravelExpenses.AsNoTracking().Where(x => x.TravelRequestId == parent.Id && !x.IsDeleted)
            .GroupBy(x => new { x.Currency, x.Category, x.Status })
            .Select(g => new { g.Key.Currency, g.Key.Category, g.Key.Status, Amount = g.Sum(x => x.Amount) }).ToListAsync(ct);
        var groups = sums.GroupBy(x => NormalizeCurrency(x.Currency)).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                decimal Approved(string category) => g.Where(x => x.Status == "Approved" && x.Category == category).Sum(x => x.Amount);
                var actual = new TravelExpenseActualDto { Travel = Approved("Travel"), Hotel = Approved("Hotel"),
                    LocalTransport = Approved("LocalTransport"), Hospitality = Approved("Hospitality"), Other = Approved("Other") };
                actual.Total = actual.Travel + actual.Hotel + actual.LocalTransport + actual.Hospitality + actual.Other;
                return new TravelExpenseCurrencySummaryDto { Currency = g.Key, Actual = actual,
                    DraftTotal = g.Where(x => x.Status == "Draft").Sum(x => x.Amount),
                    SubmittedTotal = g.Where(x => x.Status == "Submitted").Sum(x => x.Amount),
                    ApprovedTotal = actual.Total, RejectedTotal = g.Where(x => x.Status == "Rejected").Sum(x => x.Amount) };
            }).ToList();
        return new() { TravelRequestId = parent.Id, TravelReferenceNo = parent.ReferenceNo,
            Estimated = new() { Currency = NormalizeCurrency(parent.Currency), Travel = parent.EstimatedTravelCost,
                Hotel = parent.EstimatedHotelCost, LocalTransport = parent.EstimatedLocalTransportCost,
                Hospitality = parent.EstimatedHospitalityCost, Total = TravelRequestService.CalculateTotalEstimatedCost(parent) },
            ActualByCurrency = groups };
    }

    private static void EnsureReady(TravelRequest parent)
    {
        if (parent.BusinessState != "Upcoming" || (parent.ApprovalRequired
            ? parent.ApprovalState != "Approved" : parent.ApprovalState != "NotRequired"))
            throw new BusinessRuleException("Travel request is not ready for expense tracking.");
    }

    private async Task<TravelRequest> ParentAsync(long id, bool locked, CancellationToken ct)
    {
        TravelRequest? parent;
        if (locked && db.Database.IsRelational())
        {
            var rows = await db.TravelRequests.FromSqlInterpolated(
                $"SELECT * FROM public.ea_travel_requests WHERE \"Id\" = {id} AND NOT \"IsDeleted\" FOR UPDATE").ToListAsync(ct);
            parent = rows.SingleOrDefault();
            if (parent != null) await db.Entry(parent).ReloadAsync(ct);
        }
        else parent = await db.TravelRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        return parent ?? throw new NotFoundException($"Travel request {id} not found.");
    }

    private async Task<(TravelExpense Row, TravelRequest Parent)> LockExpenseAsync(long id, CancellationToken ct)
    {
        var parentId = await db.TravelExpenses.AsNoTracking().Where(x => x.Id == id && !x.IsDeleted)
            .Select(x => (long?)x.TravelRequestId).SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Travel expense not found.");
        var parent = await ParentAsync(parentId, true, ct);
        var row = await db.TravelExpenses.SingleOrDefaultAsync(x => x.Id == id && x.TravelRequestId == parent.Id && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Travel expense not found.");
        if (db.Database.IsRelational()) await db.Entry(row).ReloadAsync(ct);
        if (row.IsDeleted) throw new NotFoundException("Travel expense not found.");
        return (row, parent);
    }

    private async Task ValidateReceiptAsync(long parentId, long? receiptId, CancellationToken ct)
    {
        if (!receiptId.HasValue) return;
        var parentKey = parentId.ToString(CultureInfo.InvariantCulture);
        if (!await db.Attachments.AsNoTracking().AnyAsync(a => a.Id == receiptId && a.RelatedModule == "Travel"
            && a.RelatedEntity == "TravelRequest" && a.RelatedEntityId == parentKey && a.IsActive && !a.IsDeleted, ct))
            throw new BadRequestException("Receipt must be an active Travel document belonging to this Travel request.");
    }

    private async Task<TravelExpenseResponseDto> WithReceiptAsync(TravelExpense row, TravelRequest parent, CancellationToken ct)
    {
        var dto = ToDto(row, parent.ReferenceNo);
        if (row.ReceiptAttachmentId.HasValue)
            dto.Receipt = (await documents.ListAsync(parent.Id, ct)).SingleOrDefault(x => x.Id == row.ReceiptAttachmentId);
        return dto;
    }

    private static void Validate(SaveTravelExpenseDto dto)
    {
        if (dto.Category is not ("Travel" or "Hotel" or "LocalTransport" or "Hospitality" or "Other"))
            throw new BadRequestException("Unsupported expense category.");
        if (dto.Amount < 0 || dto.Amount > 9999999999999999.99m || decimal.Round(dto.Amount, 2) != dto.Amount)
            throw new BadRequestException("Amount must be nonnegative, fit numeric(18,2), and have at most two decimal places.");
        if (dto.Currency?.Length > 10) throw new BadRequestException("Currency must not exceed 10 characters.");
    }

    private void Touch(TravelExpense row) { row.ModifiedBy = Actor; row.ModifiedDate = Clock.UtcNowTz; }
    private void AddAudit(string action, TravelExpense row, TravelRequest parent, TravelExpenseResponseDto? previous) =>
        audit.AddAudit("TRAVEL_EXPENSE_" + action, "Travel", "TravelExpense", row.Id.ToString(CultureInfo.InvariantCulture),
            previous, ToDto(row, parent.ReferenceNo));

    private static void Apply(TravelExpense row, SaveTravelExpenseDto dto)
    {
        row.Category = dto.Category;
        row.Description = dto.Description;
        row.Amount = dto.Amount;
        row.Currency = NormalizeCurrency(dto.Currency);
        row.ExpenseDate = dto.ExpenseDate;
        row.ReceiptAttachmentId = dto.ReceiptAttachmentId;
    }
    private static TravelExpenseResponseDto ToDto(TravelExpense row, string referenceNo) => new()
    {
        ExpenseId = row.Id, TravelRequestId = row.TravelRequestId, TravelReferenceNo = referenceNo,
        Category = row.Category,
        Description = row.Description,
        Amount = row.Amount,
        Currency = row.Currency,
        ExpenseDate = row.ExpenseDate,
        ReceiptAttachmentId = row.ReceiptAttachmentId,
        Status = row.Status,
        SubmittedBy = row.SubmittedBy,
        SubmittedAt = row.SubmittedAt,
        ApprovedBy = row.ApprovedBy,
        ApprovedAt = row.ApprovedAt,
        RejectedBy = row.RejectedBy,
        RejectedAt = row.RejectedAt,
        RejectionReason = row.RejectionReason,
        CreatedBy = row.CreatedBy, CreatedAt = row.CreatedDate, UpdatedAt = row.ModifiedDate
    };
}
