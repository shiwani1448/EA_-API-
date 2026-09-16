using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public sealed class ApprovalQueryService(EaFmsDbContext db, IApprovalDocumentService documents)
{
    // Canonical catalog name already used by ApprovalService.CreateAsync.
    private const string ApprovalModuleName = "EA Approval";

    public async Task<ApprovalListResponseDto> ListAsync(string? search, string? status, string? priority, string? approver, string? requestedBy, string? department, DateTime? createdFrom, DateTime? createdTo, DateTime? requiredFrom, DateTime? requiredTo, string? dueState, int page, int pageSize, CancellationToken ct)
    {
        var approvalModuleId = await ResolveApprovalModuleIdAsync(ct);
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var query = Requests(approvalModuleId);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(x => EF.Functions.ILike(x.ReferenceNo, $"%{term}%") || (x.RequestTitle != null && EF.Functions.ILike(x.RequestTitle, $"%{term}%"))); }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.WorkflowStatus == status);
        if (!string.IsNullOrWhiteSpace(priority)) query = query.Where(x => x.Priority == priority);
        if (!string.IsNullOrWhiteSpace(approver)) query = query.Where(x => x.ApproverId == approver || x.ApproverName == approver);
        if (!string.IsNullOrWhiteSpace(requestedBy)) query = query.Where(x => x.RequestedBy == requestedBy || x.CreatedBy == requestedBy);
        if (!string.IsNullOrWhiteSpace(department)) query = query.Where(x => x.Department == department);
        if (createdFrom.HasValue) query = query.Where(x => x.CreatedAt >= createdFrom);
        if (createdTo.HasValue) query = query.Where(x => x.CreatedAt <= createdTo);
        if (requiredFrom.HasValue) query = query.Where(x => x.RequiredApprovalDate >= requiredFrom);
        if (requiredTo.HasValue) query = query.Where(x => x.RequiredApprovalDate <= requiredTo);
        var now = Clock.UtcNowTz;
        if (string.Equals(dueState, "Overdue", StringComparison.OrdinalIgnoreCase)) query = query.Where(x => x.EaTask.AllottedTatMinutes.HasValue && x.EaTask.CreatedDate.AddMinutes(x.EaTask.AllottedTatMinutes.Value) < now);
        else if (string.Equals(dueState, "Due", StringComparison.OrdinalIgnoreCase)) query = query.Where(x => x.EaTask.AllottedTatMinutes.HasValue && x.EaTask.CreatedDate.AddMinutes(x.EaTask.AllottedTatMinutes.Value) >= now);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new { Request = x, DocumentCount = db.Attachments.Count(a => a.RelatedModule == "Approval" && a.RelatedEntity == "ApprovalRequest" && a.RelatedEntityId == x.Id.ToString() && a.IsActive && !a.IsDeleted) }).ToListAsync(ct);
        return new() { TotalCount = total, Page = page, PageSize = pageSize, Items = rows.Select(x => ToList(x.Request, x.DocumentCount, now)).ToList() };
    }

    public async Task<ApprovalDetailDto?> DetailAsync(long id, CancellationToken ct)
    {
        var approvalModuleId = await ResolveApprovalModuleIdAsync(ct);
        var request = await Requests(approvalModuleId).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (request is null) return null;
        var cycles = (await db.ApprovalCycles.AsNoTracking().Where(x => x.ApprovalRequestId == id).OrderBy(x => x.CycleNo).ThenBy(x => x.Id).ToListAsync(ct)).Select(Cycle).ToList();
        var now = Clock.UtcNowTz;
        return ToDetail(request, cycles, await documents.ListAsync(id, ct), await HistoryAsync(request, ct), now);
    }

    public async Task<IReadOnlyList<ApprovalCycleDto>?> CyclesAsync(long id, CancellationToken ct)
    {
        var moduleId = await ResolveApprovalModuleIdAsync(ct);
        if (!await Requests(moduleId).AnyAsync(x => x.Id == id, ct)) return null;
        return (await db.ApprovalCycles.AsNoTracking().Where(x => x.ApprovalRequestId == id).OrderBy(x => x.CycleNo).ThenBy(x => x.Id).ToListAsync(ct)).Select(Cycle).ToList();
    }

    public async Task<IReadOnlyList<ApprovalHistoryItemDto>?> HistoryForAsync(long id, CancellationToken ct)
    {
        var moduleId = await ResolveApprovalModuleIdAsync(ct);
        var request = await Requests(moduleId).SingleOrDefaultAsync(x => x.Id == id, ct);
        return request is null ? null : await HistoryAsync(request, ct);
    }

    public async Task<ApprovalDashboardDto> DashboardAsync(CancellationToken ct)
    {
        var query = Requests(await ResolveApprovalModuleIdAsync(ct)); var now = Clock.UtcNowTz;
        return new() { TotalRequests = await query.CountAsync(ct), PendingApproval = await query.CountAsync(x => x.WorkflowStatus == "PendingApproval", ct), Approved = await query.CountAsync(x => x.WorkflowStatus == "Approved", ct), Rejected = await query.CountAsync(x => x.WorkflowStatus == "Rejected", ct), Overdue = await query.CountAsync(x => x.EaTask.AllottedTatMinutes.HasValue && x.EaTask.CreatedDate.AddMinutes(x.EaTask.AllottedTatMinutes.Value) < now, ct) };
    }

    private async Task<long> ResolveApprovalModuleIdAsync(CancellationToken ct)
    {
        var ids = await db.BusinessModules.AsNoTracking().Where(x => x.IsActive && !x.IsDeleted && x.Name == ApprovalModuleName).Select(x => x.Id).Take(2).ToListAsync(ct);
        return ids.Count switch { 1 => ids[0], 0 => throw new BusinessRuleException("EA Approval business module is not registered as active in ea_business_modules."), _ => throw new BusinessRuleException("Multiple active EA Approval business modules are configured.") };
    }

    private IQueryable<ApprovalRequest> Requests(long approvalModuleId) => db.ApprovalRequests.AsNoTracking().Include(x => x.EaTask).ThenInclude(x => x.WorkflowInstance).ThenInclude(x => x!.Status).Where(x => !x.IsDeleted && x.EaTask.BusinessModuleId == approvalModuleId && x.EaTask.IsActive && !x.EaTask.IsDeleted);

    private async Task<List<ApprovalHistoryItemDto>> HistoryAsync(ApprovalRequest request, CancellationToken ct)
    {
        var result = await db.AuditLogs.AsNoTracking().Where(x => (x.Module == "Approval" || x.Module == "EA.Approval") && x.EntityName == nameof(ApprovalRequest) && x.EntityId == request.Id.ToString()).Select(x => new ApprovalHistoryItemDto { Id = x.Id, EventType = x.ActionType, OccurredAt = x.OccurredAt == default ? x.CreatedDate : x.OccurredAt, ActorId = x.ActorId, ActorName = x.ActorName, Description = x.Description }).ToListAsync(ct);
        if (request.EaTask.WorkflowInstanceId is long workflowId) result.AddRange(await db.WorkflowHistory.AsNoTracking().Where(x => x.WorkflowInstanceId == workflowId).Select(x => new ApprovalHistoryItemDto { Id = x.Id, EventType = "WorkflowTransition", OccurredAt = x.ChangedAt, ActorId = x.StageOwnerId, ActorName = x.StageOwnerName, Description = x.Notes }).ToListAsync(ct));
        return result.OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).ToList();
    }

    private static ApprovalCycleDto Cycle(ApprovalCycle x) => new() { Id = x.Id, CycleNo = x.CycleNo, SubmittedAt = x.SubmittedAt, SubmittedBy = x.SubmittedBy, RequiredApprovalDate = x.RequiredApprovalDate, ApproverId = x.ApproverId, Status = x.Status, ChangeReason = x.ChangeReason, DecisionComment = x.DecisionComment, CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt };
    private static ApprovalListItemDto ToList(ApprovalRequest x, int documents, DateTime now) => new() { ApprovalRequestId = x.Id, EaTaskId = x.EaTaskId, ReferenceNo = x.ReferenceNo, RequestTitle = x.RequestTitle, Description = x.Description, RequestedBy = x.RequestedBy, Department = x.Department, Type = x.RequestType, Priority = x.Priority, Approver = x.ApproverName ?? x.ApproverId, RequiredApprovalDate = x.RequiredApprovalDate, WorkflowStatus = x.WorkflowStatus, DueState = Due(x.EaTask, now), CurrentCycleNo = x.CurrentCycleNo, DocumentCount = documents, CreatedAt = x.CreatedAt, SubmittedAt = x.SubmittedAt, UpdatedAt = x.UpdatedAt };
    private static ApprovalDetailDto ToDetail(ApprovalRequest x, IReadOnlyList<ApprovalCycleDto> cycles, IReadOnlyList<ApprovalDocumentResponseDto> documents, IReadOnlyList<ApprovalHistoryItemDto> history, DateTime now) => new() { ApprovalRequestId = x.Id, EaTaskId = x.EaTaskId, ReferenceNo = x.ReferenceNo, RequestTitle = x.RequestTitle, Description = x.Description, Justification = x.Justification, RequestedBy = x.RequestedBy, CreatedBy = x.CreatedBy, UpdatedBy = x.UpdatedBy, Type = x.RequestType, Priority = x.Priority, Department = x.Department, Amount = x.Amount, Currency = x.Currency, Approver = x.ApproverName ?? x.ApproverId, WorkflowStatus = x.WorkflowStatus, RequiredApprovalDate = x.RequiredApprovalDate, CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt, SubmittedAt = x.SubmittedAt, ApprovedAt = x.ApprovedAt, RejectedAt = x.RejectedAt, ClosedAt = x.ClosedAt, DueState = Due(x.EaTask, now), CurrentCycleNo = x.CurrentCycleNo, CurrentCycle = cycles.SingleOrDefault(c => c.CycleNo == x.CurrentCycleNo), LatestCycle = cycles.LastOrDefault(), Cycles = cycles, Documents = documents, History = history, Task = new() { EaTaskId = x.EaTaskId, BusinessModuleId = x.EaTask.BusinessModuleId, BusinessRecordId = x.EaTask.BusinessRecordId, Status = x.EaTask.WorkflowInstance?.Status?.Name ?? x.WorkflowStatus, Priority = x.Priority, DueDate = x.EaTask.AllottedTatMinutes.HasValue ? x.EaTask.CreatedDate.AddMinutes(x.EaTask.AllottedTatMinutes.Value) : null, AllottedTatMinutes = x.EaTask.AllottedTatMinutes, DueState = Due(x.EaTask, now) } };
    private static string Due(EaTask task, DateTime now) => !task.AllottedTatMinutes.HasValue ? "Unavailable" : task.CreatedDate.AddMinutes(task.AllottedTatMinutes.Value) < now ? "Overdue" : "Due";
}
