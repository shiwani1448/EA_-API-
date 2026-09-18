using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Travel;

/// <summary>
/// TravelRequestService.GetHistoryAsync reads purely from the shared ea_audit_logs table
/// written by the real Travel services (TravelRequestService[.Lifecycle], TravelDocumentService,
/// TravelBookingService, TravelExpenseService, TravelArrangementService). These tests wire
/// those real services against one shared InMemory db + one real AuditService instance so
/// every audit row exercised here is genuine, not hand-crafted.
/// </summary>
public class TravelHistoryTests
{
    private static EaFmsDbContext Db() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static ICurrentUserService MakeUser(string name = "reviewer", long id = 42) =>
        Mock.Of<ICurrentUserService>(u => u.UserName == name && u.UserId == id);

    private static TravelRequestService MakeTravelService(EaFmsDbContext db, ICurrentUserService user, IAuditService audit) =>
        new(db, audit, user, new Mock<ITravelNumberRepository>(MockBehavior.Strict).Object, new Mock<IEaTaskService>(MockBehavior.Strict).Object);

    private static TravelRequest SeedParent(EaFmsDbContext db, long id = 1, bool approval = true, string approverId = "mgr-1")
    {
        db.Tasks.Add(new EaTask { Id = 100 + id, BusinessModuleId = 6, ModuleName = "Travel & Hospitality", BusinessRecordId = id.ToString(),
            Task = $"TRV-{id}", ExecutionStatus = "NotStarted", IsActive = true, CreatedBy = "creator", CreatedDate = DateTime.UtcNow });
        var parent = new TravelRequest { Id = id, ReferenceNo = $"TRV-{id}", EaTaskId = 100 + id,
            BusinessState = "Draft", ApprovalRequired = approval,
            ApprovalState = approval ? "NotSubmitted" : "NotRequired",
            ApproverId = approverId, CreatedBy = "creator", CreatedDate = DateTime.UtcNow };
        db.TravelRequests.Add(parent);
        db.SaveChanges();
        return parent;
    }

    /// <summary>
    /// Seeds one faithful TRAVEL_CREATE_DRAFT audit row, matching exactly the shape
    /// TravelRequestService.CreateDraftAsync itself writes — without paying for that
    /// method's own BusinessModule/EaTask creation plumbing, which is already covered by
    /// TravelRequestServiceTests. This only exercises the History mapper's handling of it.
    /// </summary>
    private static void SeedCreateDraftAudit(IAuditService audit, TravelRequest parent)
    {
        audit.AddAudit("TRAVEL_CREATE_DRAFT", "Travel", nameof(TravelRequest),
            parent.Id.ToString(), null,
            new { parent.ReferenceNo, parent.EaTaskId, parent.BusinessState, parent.ApprovalState },
            "Travel request draft created");
    }

    // ------------------------------------------------------------
    // Ownership / isolation / not-found
    // ------------------------------------------------------------

    [Fact]
    public async Task GetHistoryAsync_UnknownParent_Throws404()
    {
        using var db = Db();
        var svc = MakeTravelService(db, MakeUser(), new AuditService(db, MakeUser()));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetHistoryAsync(999));
    }

    [Fact]
    public async Task GetHistoryAsync_DeletedParent_Throws404()
    {
        using var db = Db();
        var parent = SeedParent(db);
        parent.IsDeleted = true;
        await db.SaveChangesAsync();
        var svc = MakeTravelService(db, MakeUser(), new AuditService(db, MakeUser()));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetHistoryAsync(1));
    }

    [Fact]
    public async Task GetHistoryAsync_IsolatedToRequestedParent_NeverLeaksAnotherRequestsEvents()
    {
        using var db = Db();
        var user = MakeUser();
        var audit = new AuditService(db, user);
        var svc = MakeTravelService(db, user, audit);
        SeedParent(db, id: 1, approval: false);
        SeedParent(db, id: 2, approval: false);

        await svc.SubmitAsync(1);
        await svc.SubmitAsync(2);

        var history1 = await svc.GetHistoryAsync(1);
        Assert.All(history1, e => Assert.Equal(1, e.TravelRequestId));
        Assert.All(history1, e => Assert.Equal("TRV-1", e.TravelReferenceNo));
        Assert.DoesNotContain(history1, e => e.TravelRequestId != 1);
    }

    [Fact]
    public async Task GetHistoryAsync_DoesNotWriteANewAuditEvent()
    {
        using var db = Db();
        var user = MakeUser();
        var audit = new AuditService(db, user);
        var svc = MakeTravelService(db, user, audit);
        SeedParent(db, approval: false);
        await svc.SubmitAsync(1);

        var before = await db.AuditLogs.CountAsync();
        await svc.GetHistoryAsync(1);
        await svc.GetHistoryAsync(1);
        var after = await db.AuditLogs.CountAsync();

        Assert.Equal(before, after);
    }

    // ------------------------------------------------------------
    // Core approval-cycle lifecycle: ordering, stateType, previous/new, cycleNo, comment
    // ------------------------------------------------------------

    [Fact]
    public async Task GetHistoryAsync_ApprovalLifecycle_MapsEveryEventCorrectly_OldestFirst()
    {
        using var db = Db();
        var user = MakeUser();
        var audit = new AuditService(db, user);
        var svc = MakeTravelService(db, user, audit);
        var parent = SeedParent(db, approval: true);
        SeedCreateDraftAudit(audit, parent);
        await db.SaveChangesAsync();

        await svc.SubmitAsync(1);                                            // NotSubmitted -> Pending, cycle 1
        await svc.RequestChangesAsync(1, new() { ExpectedCycleNo = 1, ChangeReason = "Fix dates" });
        await svc.ResubmitAsync(1, new() { ExpectedCycleNo = 1, ChangesMade = "Dates fixed" });
        await svc.ApproveAsync(1, new() { ExpectedCycleNo = 2, DecisionComment = "Looks good" });
        await svc.StartAsync(1);
        await svc.CompleteAsync(1);

        var history = await svc.GetHistoryAsync(1);

        // Oldest -> newest, and nothing dropped.
        var actions = history.Select(e => e.Action).ToList();
        Assert.Equal(new[]
        {
            "TRAVEL_CREATE_DRAFT", "TRAVEL_SUBMIT", "TRAVEL_REQUEST_CHANGES", "TRAVEL_RESUBMIT",
            "TRAVEL_APPROVE", "TRAVEL_START", "TRAVEL_COMPLETE"
        }, actions);
        for (var i = 1; i < history.Count; i++)
            Assert.True(history[i].PerformedAt >= history[i - 1].PerformedAt);

        // Every event belongs to this Travel request's identity.
        Assert.All(history, e => Assert.Equal(1, e.TravelRequestId));
        Assert.All(history, e => Assert.Equal("TRV-1", e.TravelReferenceNo));
        Assert.All(history, e => Assert.Equal(101, e.EaTaskId));
        Assert.All(history, e => Assert.Equal("reviewer", e.PerformedBy));

        var byAction = history.ToDictionary(e => e.Action);

        var create = byAction["TRAVEL_CREATE_DRAFT"];
        Assert.Equal("TravelBusiness", create.StateType);
        Assert.Null(create.PreviousStatus);
        Assert.Equal("Draft", create.NewStatus);
        Assert.Null(create.CycleNo);

        var submit = byAction["TRAVEL_SUBMIT"];
        Assert.Equal("TravelApproval", submit.StateType);
        Assert.Equal("NotSubmitted", submit.PreviousStatus);
        Assert.Equal("Pending", submit.NewStatus);
        Assert.Equal(1, submit.CycleNo);

        var changes = byAction["TRAVEL_REQUEST_CHANGES"];
        Assert.Equal("TravelApproval", changes.StateType);
        Assert.Equal("Pending", changes.PreviousStatus);
        Assert.Equal("ChangesRequested", changes.NewStatus);
        Assert.Equal(1, changes.CycleNo);
        Assert.Equal("Fix dates", changes.Comment);

        var resubmit = byAction["TRAVEL_RESUBMIT"];
        Assert.Equal("TravelApproval", resubmit.StateType);
        Assert.Equal("ChangesRequested", resubmit.PreviousStatus);
        Assert.Equal("Pending", resubmit.NewStatus);
        Assert.Equal(2, resubmit.CycleNo);
        Assert.Equal("Dates fixed", resubmit.Comment);

        var approve = byAction["TRAVEL_APPROVE"];
        Assert.Equal("TravelApproval", approve.StateType);
        Assert.Equal("Pending", approve.PreviousStatus);
        Assert.Equal("Approved", approve.NewStatus);
        Assert.Equal(2, approve.CycleNo);
        Assert.Equal("Looks good", approve.Comment);

        var start = byAction["TRAVEL_START"];
        Assert.Equal("TravelBusiness", start.StateType);
        Assert.Equal("Upcoming", start.PreviousStatus);
        Assert.Equal("Active", start.NewStatus);
        Assert.Null(start.CycleNo); // no approval cycle concept applies here — not guessed

        var complete = byAction["TRAVEL_COMPLETE"];
        Assert.Equal("TravelBusiness", complete.StateType);
        Assert.Equal("Active", complete.PreviousStatus);
        Assert.Equal("Completed", complete.NewStatus);
        Assert.Null(complete.CycleNo);

        // Internal identifiers are never exposed to the frontend contract.
        Assert.All(history, e => Assert.DoesNotContain(typeof(TravelHistoryEventDto).GetProperties(),
            p => p.Name is "TravelRequestCycleId" or "WorkflowInstanceId"));
    }

    [Fact]
    public async Task GetHistoryAsync_NoApprovalSubmit_MapsAsTravelBusinessDraftToUpcoming()
    {
        using var db = Db();
        var user = MakeUser();
        var audit = new AuditService(db, user);
        var svc = MakeTravelService(db, user, audit);
        SeedParent(db, approval: false);

        await svc.SubmitAsync(1);
        var submit = (await svc.GetHistoryAsync(1)).Single(e => e.Action == "TRAVEL_SUBMIT");

        Assert.Equal("TravelBusiness", submit.StateType);
        Assert.Equal("Draft", submit.PreviousStatus);
        Assert.Equal("Upcoming", submit.NewStatus);
        Assert.Null(submit.CycleNo);
    }

    [Fact]
    public async Task GetHistoryAsync_Cancel_MapsPreviousStatusFromTheActualPriorBusinessState()
    {
        using var db = Db();
        var user = MakeUser();
        var audit = new AuditService(db, user);
        var svc = MakeTravelService(db, user, audit);
        SeedParent(db, approval: false);
        await svc.SubmitAsync(1); // -> Upcoming

        await svc.CancelAsync(1);
        var cancel = (await svc.GetHistoryAsync(1)).Single(e => e.Action == "TRAVEL_CANCEL");

        Assert.Equal("TravelBusiness", cancel.StateType);
        Assert.Equal("Upcoming", cancel.PreviousStatus);
        Assert.Equal("Cancelled", cancel.NewStatus);
    }

    // ------------------------------------------------------------
    // Child-record events: Booking / Expense / Hospitality / LocalTransport / Documents
    // ------------------------------------------------------------

    [Fact]
    public async Task GetHistoryAsync_BookingEvents_MapStateTypeAndMetadata_WithNullCycleNo()
    {
        using var db = Db();
        var user = MakeUser();
        var audit = new AuditService(db, user);
        var travelSvc = MakeTravelService(db, user, audit);
        SeedParent(db, approval: false);
        await travelSvc.SubmitAsync(1); // BusinessState Upcoming — required for booking eligibility

        var bookingSvc = new TravelBookingService(db, user, audit);
        var booking = await bookingSvc.CreateAsync(1, new() { BookingType = "Flight" });
        await bookingSvc.UpdateAsync(booking.BookingId, new() { BookingStatus = "InProgress" });
        await bookingSvc.UpdateAsync(booking.BookingId, new() { BookingStatus = "Booked", Notes = "Confirmed with airline" });

        var history = await travelSvc.GetHistoryAsync(1);
        var created = history.Single(e => e.Action == "TRAVEL_BOOKING_CREATE");
        var updates = history.Where(e => e.Action == "TRAVEL_BOOKING_UPDATE").ToList();
        var toBooked = updates.Single(e => e.NewStatus == "Booked");

        Assert.Equal("Booking", created.StateType);
        Assert.Null(created.PreviousStatus);
        Assert.Equal("Requested", created.NewStatus);
        Assert.Null(created.CycleNo);
        Assert.Equal(booking.BookingId, created.Metadata!["bookingId"]);
        Assert.Equal("Flight", created.Metadata!["bookingType"]);

        Assert.Equal(2, updates.Count);
        Assert.Equal("Booking", toBooked.StateType);
        Assert.Equal("InProgress", toBooked.PreviousStatus);
        Assert.Equal("Booked", toBooked.NewStatus);
        Assert.Equal("Confirmed with airline", toBooked.Comment);
    }

    [Fact]
    public async Task GetHistoryAsync_ExpenseEvents_MapStatusTransitionsAndRejectionReason()
    {
        using var db = Db();
        var user = MakeUser();
        var audit = new AuditService(db, user);
        var travelSvc = MakeTravelService(db, user, audit);
        SeedParent(db, approval: false);
        await travelSvc.SubmitAsync(1);

        var expenseSvc = new TravelExpenseService(db, user, audit, new Mock<ITravelDocumentService>(MockBehavior.Strict).Object);
        var expense = await expenseSvc.CreateAsync(1, new() { Category = "Travel", Amount = 50m });
        await expenseSvc.SubmitAsync(expense.ExpenseId);
        await expenseSvc.RejectAsync(expense.ExpenseId, new() { RejectionReason = "Missing receipt" });

        var history = await travelSvc.GetHistoryAsync(1);
        var create = history.Single(e => e.Action == "TRAVEL_EXPENSE_CREATE");
        var submit = history.Single(e => e.Action == "TRAVEL_EXPENSE_SUBMIT");
        var reject = history.Single(e => e.Action == "TRAVEL_EXPENSE_REJECT");

        Assert.Equal("Expense", create.StateType);
        Assert.Equal("Draft", create.NewStatus);
        Assert.Equal(expense.ExpenseId, create.Metadata!["expenseId"]);

        Assert.Equal("Expense", submit.StateType);
        Assert.Equal("Draft", submit.PreviousStatus);
        Assert.Equal("Submitted", submit.NewStatus);

        Assert.Equal("Expense", reject.StateType);
        Assert.Equal("Submitted", reject.PreviousStatus);
        Assert.Equal("Rejected", reject.NewStatus);
        Assert.Equal("Missing receipt", reject.Comment);
    }

    [Fact]
    public async Task GetHistoryAsync_HospitalityAndLocalTransportEvents_MapStateTypes()
    {
        using var db = Db();
        var user = MakeUser();
        var audit = new AuditService(db, user);
        var travelSvc = MakeTravelService(db, user, audit);
        SeedParent(db, approval: false);
        await travelSvc.SubmitAsync(1);

        var arrangementSvc = new TravelArrangementService(db, user, audit);
        var hospitality = await arrangementSvc.CreateHospitalityAsync(1, new());
        await arrangementSvc.UpdateHospitalityAsync(hospitality.HospitalityId, new() { Status = "Scheduled" });
        var transport = await arrangementSvc.CreateLocalTransportAsync(1, new());
        await arrangementSvc.UpdateLocalTransportAsync(transport.LocalTransportId, new() { TransportStatus = "Scheduled" });

        var history = await travelSvc.GetHistoryAsync(1);

        var hospCreate = history.Single(e => e.Action == "TRAVEL_HOSPITALITY_CREATE");
        var hospUpdate = history.Single(e => e.Action == "TRAVEL_HOSPITALITY_UPDATE");
        Assert.Equal("Hospitality", hospCreate.StateType);
        Assert.Equal(hospitality.HospitalityId, hospCreate.Metadata!["hospitalityId"]);
        Assert.Equal("Hospitality", hospUpdate.StateType);
        Assert.Equal("Requested", hospUpdate.PreviousStatus);
        Assert.Equal("Scheduled", hospUpdate.NewStatus);

        var ltCreate = history.Single(e => e.Action == "TRAVEL_LOCAL_TRANSPORT_CREATE");
        var ltUpdate = history.Single(e => e.Action == "TRAVEL_LOCAL_TRANSPORT_UPDATE");
        Assert.Equal("LocalTransport", ltCreate.StateType);
        Assert.Equal(transport.LocalTransportId, ltCreate.Metadata!["localTransportId"]);
        Assert.Equal("LocalTransport", ltUpdate.StateType);
        Assert.Equal("Requested", ltUpdate.PreviousStatus);
        Assert.Equal("Scheduled", ltUpdate.NewStatus);
    }

    [Fact]
    public async Task GetHistoryAsync_DocumentEvents_MapCategoryAndDeleteMetadata_NoCycleForDraftUpload()
    {
        using var db = Db();
        var user = MakeUser();
        var audit = new AuditService(db, user);
        var travelSvc = MakeTravelService(db, user, audit);
        SeedParent(db, approval: false); // stays Draft: CurrentCycleNo == 0

        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(temp);
        try
        {
            var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == temp);
            var docSvc = new TravelDocumentService(db, user, audit, env);
            var ms = new Microsoft.AspNetCore.Http.FormFile(new MemoryStream(new byte[] { 1, 2, 3 }), 0, 3, "file", "itinerary.pdf")
            { Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(), ContentType = "application/pdf" };
            var doc = await docSvc.UploadAsync(1, ms, "Itinerary");
            await docSvc.DeleteAsync(doc.Id);

            var history = await travelSvc.GetHistoryAsync(1);
            var upload = history.Single(e => e.Action == "TRAVEL_DOCUMENT_UPLOAD");
            var delete = history.Single(e => e.Action == "TRAVEL_DOCUMENT_DELETE");

            Assert.Null(upload.CycleNo); // Draft, no cycle yet — not guessed
            Assert.Equal("Itinerary", upload.Metadata!["category"]);
            Assert.Equal(doc.Id, delete.Metadata!["documentId"]);
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { /* best-effort cleanup */ }
        }
    }
}
