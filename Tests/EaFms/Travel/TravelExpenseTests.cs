using System.Text.Json;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Travel;

public class TravelExpenseTests
{
    private static EaFmsDbContext Db() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
    private static TravelExpenseService Service(EaFmsDbContext db, string actor = "ea")
    {
        var user = Mock.Of<ICurrentUserService>(u => u.UserName == actor && u.UserId == 42);
        var audit = new AuditService(db, user);
        return new(db, user, audit, new TravelDocumentService(db, user, audit, Mock.Of<IWebHostEnvironment>(), Mock.Of<IEaActorResolver>()));
    }
    private static TravelRequest Parent(long id = 1, bool required = true, string state = "Approved", string business = "Upcoming") => new()
    {
        Id = id, EaTaskId = 100 + id, ReferenceNo = $"TRV-{id}", ApprovalRequired = required,
        ApprovalState = state, BusinessState = business, CurrentCycleNo = required ? 1 : 0,
        EstimatedTravelCost = 100, EstimatedHotelCost = 200, EstimatedLocalTransportCost = 30,
        EstimatedHospitalityCost = 40, Currency = "INR", CreatedBy = "ea", CreatedDate = DateTime.UtcNow
    };
    private static SaveTravelExpenseDto Input(string category = "Travel", decimal amount = 12.34m, string? currency = "INR") =>
        new() { Category = category, Amount = amount, Currency = currency };
    private static Attachment Receipt(string parent = "1") => new()
    {
        RelatedModule = "Travel", RelatedEntity = "TravelRequest", RelatedEntityId = parent,
        OriginalFileName = "receipt.pdf", ContentType = "application/pdf", Size = 100,
        ObjectKey = "Content/Travel/private.pdf", AccessUrl = "/Content/Travel/private.pdf"
    };

    [Theory]
    [InlineData("Travel")]
    [InlineData("Hotel")]
    [InlineData("LocalTransport")]
    [InlineData("Hospitality")]
    [InlineData("Other")]
    public async Task Create_AllCategories_DecimalAndOptionalFields(string category)
    {
        using var db = Db(); db.TravelRequests.Add(Parent()); await db.SaveChangesAsync();
        var row = await Service(db).CreateAsync(1, Input(category));
        Assert.Equal(category, row.Category); Assert.Equal(12.34m, row.Amount); Assert.Equal("Draft", row.Status);
        Assert.Equal(1, row.TravelRequestId); Assert.Equal("TRV-1", row.TravelReferenceNo);
        Assert.Null(row.Description); Assert.Null(row.ExpenseDate); Assert.Null(row.Receipt); Assert.Null(row.SubmittedAt);
        Assert.Null(row.ApprovedBy); Assert.Null(row.RejectedBy); Assert.Equal("ea", row.CreatedBy);
    }

    [Fact]
    public async Task Create_SubmittedNotRequired_IsAllowed()
    {
        using var db = Db(); db.TravelRequests.Add(Parent(required: false, state: "NotRequired")); await db.SaveChangesAsync();
        Assert.Equal("Draft", (await Service(db).CreateAsync(1, Input(amount: 0, currency: null))).Status);
    }

    [Theory]
    [InlineData(true, "NotSubmitted", "Draft")]
    [InlineData(true, "Pending", "Draft")]
    [InlineData(true, "ChangesRequested", "Draft")]
    [InlineData(true, "Rejected", "Draft")]
    [InlineData(false, "NotRequired", "Draft")]
    [InlineData(true, "Approved", "Draft")]
    public async Task CreationRequiresOperationalTravel(bool required, string state, string business)
    {
        using var db = Db(); db.TravelRequests.Add(Parent(required: required, state: state, business: business)); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).CreateAsync(1, Input()));
        Assert.Empty(db.TravelExpenses); Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public async Task Lifecycle_ReworkResubmit_AttributionAuditAndIndependentCosts()
    {
        using var db = Db(); var parent = Parent(); db.TravelRequests.Add(parent);
        db.TravelBookings.Add(new() { TravelRequestId = 1, BookingType = "Flight", Cost = 999, Currency = "INR" });
        db.TravelLocalTransports.Add(new() { TravelRequestId = 1, EstimatedCost = 888, ActualCost = 777 });
        db.TravelHospitalityArrangements.Add(new() { TravelRequestId = 1, EstimatedCost = 666, ActualCost = 555 });
        await db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(parent); var svc = Service(db);
        var row = await svc.CreateAsync(1, Input());
        var edited = await svc.UpdateAsync(row.ExpenseId, Input("Hotel", 50.75m));
        Assert.Equal("Draft", edited.Status); Assert.Equal(50.75m, edited.Amount);
        var submitted = await svc.SubmitAsync(row.ExpenseId);
        Assert.Equal("Submitted", submitted.Status); Assert.Equal("ea", submitted.SubmittedBy); Assert.NotNull(submitted.SubmittedAt);
        var reviewer = Service(db, "reviewer");
        var rejected = await reviewer.RejectAsync(row.ExpenseId, new() { RejectionReason = "Correct amount" });
        Assert.Equal("reviewer", rejected.RejectedBy); Assert.NotNull(rejected.RejectedAt);
        Assert.Null(rejected.ApprovedBy); Assert.Null(rejected.ApprovedAt);
        var corrected = await svc.UpdateAsync(row.ExpenseId, Input("Hotel", 45.50m));
        Assert.Equal("Rejected", corrected.Status); Assert.Equal("Correct amount", corrected.RejectionReason);
        var resubmitted = await svc.SubmitAsync(row.ExpenseId);
        Assert.Equal("Submitted", resubmitted.Status); Assert.Null(resubmitted.RejectedBy);
        Assert.Null(resubmitted.RejectedAt); Assert.Null(resubmitted.RejectionReason);
        var approved = await reviewer.ApproveAsync(row.ExpenseId);
        Assert.Equal("Approved", approved.Status); Assert.Equal("reviewer", approved.ApprovedBy); Assert.NotNull(approved.ApprovedAt);
        Assert.Equal(new[] { "TRAVEL_EXPENSE_CREATE", "TRAVEL_EXPENSE_UPDATE", "TRAVEL_EXPENSE_SUBMIT", "TRAVEL_EXPENSE_REJECT",
            "TRAVEL_EXPENSE_UPDATE", "TRAVEL_EXPENSE_SUBMIT", "TRAVEL_EXPENSE_APPROVE" },
            await db.AuditLogs.OrderBy(a => a.Id).Select(a => a.ActionType).ToArrayAsync());
        var rejectionAudit = await db.AuditLogs.SingleAsync(a => a.ActionType == "TRAVEL_EXPENSE_REJECT");
        Assert.Contains("Correct amount", rejectionAudit.NewValues!); Assert.Contains("Submitted", rejectionAudit.OldValues!);
        Assert.Contains("50.75", rejectionAudit.NewValues!); Assert.Contains("TRV-1", rejectionAudit.NewValues!);
        Assert.Equal(before, JsonSerializer.Serialize(await db.TravelRequests.AsNoTracking().SingleAsync()));
        Assert.Equal(999m, db.TravelBookings.Single().Cost);
        Assert.Equal(888m, db.TravelLocalTransports.Single().EstimatedCost); Assert.Equal(777m, db.TravelLocalTransports.Single().ActualCost);
        Assert.Equal(666m, db.TravelHospitalityArrangements.Single().EstimatedCost); Assert.Equal(555m, db.TravelHospitalityArrangements.Single().ActualCost);
        Assert.Empty(db.Tasks); Assert.Empty(db.WorkflowInstances); Assert.Empty(db.ApprovalRequests);
        Assert.Empty(db.ApprovalCycles); Assert.Empty(db.TravelRequestCycles);
    }

    [Theory]
    [InlineData("Draft", "approve")]
    [InlineData("Draft", "reject")]
    [InlineData("Submitted", "submit")]
    [InlineData("Approved", "submit")]
    [InlineData("Approved", "approve")]
    [InlineData("Approved", "reject")]
    [InlineData("Rejected", "approve")]
    [InlineData("Rejected", "reject")]
    public async Task InvalidAndRepeatedTransitionsConflict(string state, string action)
    {
        using var db = Db(); db.TravelRequests.Add(Parent());
        db.TravelExpenses.Add(new() { TravelRequestId = 1, Category = "Travel", Status = state }); await db.SaveChangesAsync();
        var id = db.TravelExpenses.Single().Id; var svc = Service(db);
        await Assert.ThrowsAsync<BusinessRuleException>(() => action == "submit" ? svc.SubmitAsync(id)
            : action == "approve" ? svc.ApproveAsync(id) : svc.RejectAsync(id, null));
        Assert.Equal(state, db.TravelExpenses.Single().Status); Assert.Empty(db.AuditLogs);
    }

    [Theory]
    [InlineData("Submitted")]
    [InlineData("Approved")]
    public async Task FinancialEditsBlockedAfterSubmission(string state)
    {
        using var db = Db(); db.TravelRequests.Add(Parent());
        db.TravelExpenses.Add(new() { TravelRequestId = 1, Category = "Travel", Amount = 10, Status = state }); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).UpdateAsync(db.TravelExpenses.Single().Id, Input(amount: 999)));
        Assert.Equal(10m, db.TravelExpenses.Single().Amount); Assert.Empty(db.AuditLogs);
    }

    [Theory]
    [InlineData("negative")]
    [InlineData("scale")]
    [InlineData("overflow")]
    [InlineData("currency")]
    [InlineData("category")]
    public async Task InvalidFinancialInputRejected(string invalid)
    {
        using var db = Db(); db.TravelRequests.Add(Parent()); await db.SaveChangesAsync(); var svc = Service(db);
        var row = await svc.CreateAsync(1, Input());
        var dto = Input();
        if (invalid == "negative") dto.Amount = -1;
        if (invalid == "scale") dto.Amount = 1.001m;
        if (invalid == "overflow") dto.Amount = 10000000000000000m;
        if (invalid == "currency") dto.Currency = new string('X', 11);
        if (invalid == "category") dto.Category = "Invalid";
        await Assert.ThrowsAsync<BadRequestException>(() => svc.CreateAsync(1, dto));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.UpdateAsync(row.ExpenseId, dto));
        Assert.Single(db.TravelExpenses); Assert.Single(db.AuditLogs);
    }

    [Theory]
    [InlineData("foreign-parent")]
    [InlineData("foreign-module")]
    [InlineData("foreign-entity")]
    [InlineData("deleted")]
    [InlineData("inactive")]
    [InlineData("missing")]
    public async Task ReceiptMustBeAnActiveDocumentOfSameTravel(string scenario)
    {
        using var db = Db(); db.TravelRequests.AddRange(Parent(), Parent(2));
        var att = Receipt();
        if (scenario == "foreign-parent") att.RelatedEntityId = "2";
        if (scenario == "foreign-module") att.RelatedModule = "Meeting";
        if (scenario == "foreign-entity") att.RelatedEntity = "ApprovalRequest";
        if (scenario == "deleted") att.IsDeleted = true;
        if (scenario == "inactive") att.IsActive = false;
        db.Attachments.Add(att); await db.SaveChangesAsync(); var svc = Service(db);
        var row = await svc.CreateAsync(1, Input()); var dto = Input(); dto.ReceiptAttachmentId = scenario == "missing" ? 999 : att.Id;
        await Assert.ThrowsAsync<BadRequestException>(() => svc.CreateAsync(1, dto));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.UpdateAsync(row.ExpenseId, dto));
        Assert.Null(db.TravelExpenses.Single().ReceiptAttachmentId);
    }

    [Fact]
    public async Task ReceiptMetadataUsesDocumentContract_DeletedReceiptUnavailableAndRevalidatedOnSubmit()
    {
        using var db = Db(); db.TravelRequests.Add(Parent()); var att = Receipt(); db.Attachments.Add(att); await db.SaveChangesAsync();
        var svc = Service(db); var dto = Input(); dto.ReceiptAttachmentId = att.Id;
        var row = await svc.CreateAsync(1, dto);
        Assert.Equal(att.Id, row.ReceiptAttachmentId); Assert.Equal("receipt.pdf", row.Receipt!.OriginalFileName);
        Assert.Equal($"/api/ea/travel/documents/{att.Id}", row.Receipt.DownloadUrl);
        Assert.DoesNotContain("Content/", JsonSerializer.Serialize(row));
        Assert.Equal(att.Id, (await svc.ListAsync(1)).Single().Receipt!.Id);
        att.IsDeleted = true; await db.SaveChangesAsync();
        Assert.Null((await svc.GetAsync(row.ExpenseId)).Receipt);
        await Assert.ThrowsAsync<BadRequestException>(() => svc.SubmitAsync(row.ExpenseId));
        Assert.Equal("Draft", db.TravelExpenses.Single().Status);
        await svc.UpdateAsync(row.ExpenseId, Input()); // Receipt is optional; remove unavailable reference.
        await svc.SubmitAsync(row.ExpenseId);
        Assert.Single(db.Attachments);
    }

    [Fact]
    public async Task OwnershipServerFieldsAndListOrderingArePreserved()
    {
        using var db = Db(); db.TravelRequests.AddRange(Parent(), Parent(2)); await db.SaveChangesAsync(); var svc = Service(db);
        var firstDto = Input(); firstDto.ExpenseDate = new DateTime(2026, 1, 1);
        var first = await svc.CreateAsync(1, firstDto); var second = await svc.CreateAsync(1, Input());
        await svc.CreateAsync(2, Input());
        Assert.Equal(new[] { second.ExpenseId, first.ExpenseId }, (await svc.ListAsync(1)).Select(x => x.ExpenseId));
        var dto = JsonSerializer.Deserialize<SaveTravelExpenseDto>(
            "{\"category\":\"Hotel\",\"amount\":20,\"travelRequestId\":2,\"status\":\"Approved\",\"approvedBy\":\"spoof\",\"createdBy\":\"spoof\"}",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var updated = await svc.UpdateAsync(first.ExpenseId, dto);
        Assert.Equal(1, updated.TravelRequestId); Assert.Equal("TRV-1", updated.TravelReferenceNo);
        Assert.Equal("Draft", updated.Status); Assert.Null(updated.ApprovedBy); Assert.Equal("ea", updated.CreatedBy);
        Assert.Equal(20m, (await svc.GetAsync(first.ExpenseId)).Amount);
    }

    [Fact]
    public async Task Summary_UsesOnlyRequestEstimatesAndApprovedLedger_GroupedByCurrency()
    {
        using var db = Db(); db.TravelRequests.AddRange(Parent(), Parent(2));
        db.TravelBookings.Add(new() { TravelRequestId = 1, BookingType = "Hotel", Cost = 999999 });
        db.TravelLocalTransports.Add(new() { TravelRequestId = 1, ActualCost = 888888 });
        db.TravelHospitalityArrangements.Add(new() { TravelRequestId = 1, ActualCost = 777777 });
        foreach (var (category, amount) in new[] { ("Travel", 10.10m), ("Hotel", 20.20m), ("LocalTransport", 30.30m), ("Hospitality", 40.40m), ("Other", 50.50m) })
            db.TravelExpenses.Add(new() { TravelRequestId = 1, Category = category, Amount = amount, Currency = "INR", Status = "Approved" });
        foreach (var state in new[] { "Draft", "Submitted", "Rejected" })
            db.TravelExpenses.Add(new() { TravelRequestId = 1, Category = "Other", Amount = 5m, Currency = "INR", Status = state });
        db.TravelExpenses.AddRange(
            new() { TravelRequestId = 1, Category = "Travel", Amount = 1m, Currency = " inr ", Status = "Approved" },
            new() { TravelRequestId = 1, Category = "Travel", Amount = 7m, Currency = "USD", Status = "Approved" },
            new() { TravelRequestId = 1, Category = "Other", Amount = 8m, Currency = null, Status = "Approved" },
            new() { TravelRequestId = 1, Category = "Other", Amount = 2m, Currency = " ", Status = "Approved" },
            new() { TravelRequestId = 1, Category = "Other", Amount = 999m, Currency = "INR", Status = "Approved", IsDeleted = true },
            new() { TravelRequestId = 2, Category = "Other", Amount = 999m, Currency = "INR", Status = "Approved" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var result = await Service(db).SummaryAsync(1);
        Assert.Equal("TRV-1", result.TravelReferenceNo); Assert.Equal("INR", result.Estimated.Currency);
        Assert.Equal(100m, result.Estimated.Travel); Assert.Equal(200m, result.Estimated.Hotel);
        Assert.Equal(30m, result.Estimated.LocalTransport); Assert.Equal(40m, result.Estimated.Hospitality);
        Assert.Equal(370m, result.Estimated.Total); Assert.Equal(3, result.ActualByCurrency.Count);
        var inr = result.ActualByCurrency.Single(x => x.Currency == "INR");
        Assert.Equal(11.10m, inr.Actual.Travel); Assert.Equal(20.20m, inr.Actual.Hotel);
        Assert.Equal(30.30m, inr.Actual.LocalTransport); Assert.Equal(40.40m, inr.Actual.Hospitality);
        Assert.Equal(50.50m, inr.Actual.Other); Assert.Equal(152.50m, inr.Actual.Total);
        Assert.Equal(152.50m, inr.ApprovedTotal); Assert.Equal(5m, inr.DraftTotal); Assert.Equal(5m, inr.SubmittedTotal); Assert.Equal(5m, inr.RejectedTotal);
        Assert.Equal(7m, result.ActualByCurrency.Single(x => x.Currency == "USD").Actual.Total);
        Assert.Equal(10m, result.ActualByCurrency.Single(x => x.Currency == null).Actual.Total);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task CurrencyIsNormalizedAndUnknownIsNotInferred_EmptySummarySupportsNullEstimates()
    {
        using var db = Db(); var parent = Parent(); parent.EstimatedTravelCost = null; db.TravelRequests.Add(parent); await db.SaveChangesAsync();
        var svc = Service(db); var summary = await svc.SummaryAsync(1);
        Assert.Empty(summary.ActualByCurrency); Assert.Null(summary.Estimated.Travel); Assert.Equal(270m, summary.Estimated.Total);
        Assert.Equal("USD", (await svc.CreateAsync(1, Input(currency: " usd "))).Currency);
        Assert.Null((await svc.CreateAsync(1, Input(currency: " "))).Currency);
    }

    [Fact]
    public async Task MissingDeletedExpenseAndParentAreNotFound()
    {
        using var db = Db(); var svc = Service(db);
        await Assert.ThrowsAsync<NotFoundException>(() => svc.CreateAsync(1, Input()));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.ListAsync(1));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.SummaryAsync(1));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetAsync(99));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.SubmitAsync(99));
        var parent = Parent(); db.TravelRequests.Add(parent); await db.SaveChangesAsync();
        var row = await svc.CreateAsync(1, Input()); var other = await svc.CreateAsync(1, Input());
        (await db.TravelExpenses.FindAsync(row.ExpenseId))!.IsDeleted = true; await db.SaveChangesAsync();
        Assert.Single(await svc.ListAsync(1));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetAsync(row.ExpenseId));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.UpdateAsync(row.ExpenseId, Input()));
        parent.IsDeleted = true; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetAsync(other.ExpenseId));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.SubmitAsync(other.ExpenseId));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.SummaryAsync(1));
    }

    [Fact]
    public void ExpenseSchemaDelta_OnlyIntendedLedger_OrEmptyAfterMigration()
    {
        using var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseNpgsql("Host=localhost;Database=model_validation;Username=unused;Password=unused").Options);
        var source = db.GetService<IModelRuntimeInitializer>().Initialize(db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
        var target = db.GetService<IDesignTimeModel>().Model;
        var delta = db.GetService<IMigrationsModelDiffer>().GetDifferences(source.GetRelationalModel(), target.GetRelationalModel());
        Assert.All(delta, operation =>
        {
            if (operation is CreateTableOperation table) Assert.Equal("ea_travel_expenses", table.Name);
            else if (operation is CreateIndexOperation index) Assert.Equal("ea_travel_expenses", index.Table);
            else Assert.True(false, $"Unexpected schema change: {operation.GetType().Name}");
        });
        var entity = target.FindEntityType(typeof(TravelExpense))!;
        Assert.Equal(2, entity.GetForeignKeys().Count());
        Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.Equal(18, entity.FindProperty("Amount")!.GetPrecision()); Assert.Equal(2, entity.FindProperty("Amount")!.GetScale());
        Assert.Null(entity.FindProperty("EaTaskId")); Assert.Null(entity.FindProperty("ApprovalCycleId"));
    }
}
