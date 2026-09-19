using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Jarvis5.Validators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Travel;

// Isolated InMemory tests only: no application database, sequence or runtime API.
// These exercise state and persistence guards, not PostgreSQL locking/rollback.
public class TravelLifecycleTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        public EaFmsDbContext Db { get; } = new(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        public Mock<IAuditService> Audit { get; } = new();
        public Mock<ITravelNumberRepository> Numbers { get; } = new(MockBehavior.Strict);
        public Mock<IEaTaskService> Tasks { get; } = new(MockBehavior.Strict);
        public Mock<IEaActorResolver> ActorResolver { get; } = new(MockBehavior.Strict);
        public TravelRequestService Service { get; }
        public TravelRequest Parent { get; }
        public Fixture(bool approval = true)
        {
            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.UserId).Returns(42);
            user.SetupGet(x => x.UserName).Returns("reviewer");
            var realAudit = new AuditService(Db, user.Object);
            Audit.Setup(x => x.AddAudit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<object>(), It.IsAny<string>()))
                .Callback<string, string, string, string, object?, object?, string?>(
                    (a, m, e, id, old, next, d) => realAudit.AddAudit(a, m, e, id, old, next, d));
            Service = new(Db, Audit.Object, user.Object, Numbers.Object, Tasks.Object, ActorResolver.Object);
            Db.Tasks.Add(new EaTask { Id = 19, BusinessModuleId = 6, ModuleName = "Travel & Hospitality", BusinessRecordId = "1",
                Task = "TRV-TEST", Description = "Original purpose", ExecutionStatus = "NotStarted", IsActive = true,
                CreatedBy = "creator", CreatedDate = DateTime.UtcNow });
            Parent = new TravelRequest { Id = 1, EaTaskId = 19, ReferenceNo = "TRV-TEST",
                BusinessState = "Draft", ApprovalRequired = approval,
                ApprovalState = approval ? "NotSubmitted" : "NotRequired",
                ApproverId = "opaque-manager", ApproverNameSnapshot = null,
                CreatedBy = "creator", CreatedDate = DateTime.UtcNow };
            Db.TravelRequests.Add(Parent);
            Db.SaveChanges();
        }
        public Task<TravelActionResponseDto> Decide(string action, int cycle = 1) => action switch
        {
            "Approved" => Service.ApproveAsync(1, new() { ExpectedCycleNo = cycle }),
            "Rejected" => Service.RejectAsync(1, new() { ExpectedCycleNo = cycle }),
            _ => Service.RequestChangesAsync(1, new() { ExpectedCycleNo = cycle, ChangeReason = "Revise itinerary" })
        };
        public async Task AssertTaskUnchanged()
        {
            var task = Assert.Single(await Db.Tasks.ToListAsync());
            Assert.Equal(19, task.Id);
            Assert.Equal("1", task.BusinessRecordId);
            Assert.Equal("TRV-TEST", task.Task);
            Assert.Equal("Original purpose", task.Description);
            Assert.Null(task.AllottedTatMinutes);
            Assert.Null(task.WorkflowInstanceId);
            Assert.Empty(await Db.WorkflowInstances.ToListAsync());
            Assert.Empty(await Db.TatRules.ToListAsync());
            Assert.Empty(await Db.ApprovalRequests.ToListAsync());
            Assert.Empty(await Db.ApprovalCycles.ToListAsync());
            Numbers.VerifyNoOtherCalls();
            Tasks.VerifyNoOtherCalls();
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    [Theory]
    [InlineData(true, "Draft", "Pending", 1)]
    [InlineData(false, "Upcoming", "NotRequired", 0)]
    public async Task Submit_UsesApprovedBranchAndRejectsDuplicate(bool approval, string business, string state, int cycles)
    {
        await using var f = new Fixture(approval);
        var result = await f.Service.SubmitAsync(1);
        Assert.Equal(business, result.BusinessState);
        Assert.Equal(state, result.ApprovalState);
        Assert.Equal(cycles, result.CurrentCycleNo);
        Assert.NotNull(result.SubmittedAt);
        Assert.Equal(cycles, await f.Db.TravelRequestCycles.CountAsync());
        if (approval)
        {
            Assert.Equal("reviewer", result.CurrentCycle!.SubmittedBy);
            Assert.Equal("opaque-manager", result.CurrentCycle.ApproverId);
            Assert.Null(result.CurrentCycle.ApproverNameSnapshot);
        }
        else Assert.Null(result.CurrentCycle);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SubmitAsync(1));
        Assert.Equal("TRAVEL_SUBMIT", Assert.Single(await f.Db.AuditLogs.ToListAsync()).ActionType);
        await f.AssertTaskUnchanged();
    }

    [Theory]
    [InlineData("Approved", "Upcoming", "TRAVEL_APPROVE")]
    [InlineData("Rejected", "Draft", "TRAVEL_REJECT")]
    [InlineData("ChangesRequested", "Draft", "TRAVEL_REQUEST_CHANGES")]
    public async Task Decisions_StampCurrentCycleAndBlockAllFurtherDecisions(string decision, string business, string audit)
    {
        await using var f = new Fixture();
        await f.Service.SubmitAsync(1);
        var result = await f.Decide(decision);
        Assert.Equal(decision, result.ApprovalState);
        Assert.Equal(business, result.BusinessState);
        Assert.Equal(decision, result.CurrentCycle!.DecisionState);
        Assert.Equal("reviewer", result.CurrentCycle.DecisionBy);
        Assert.NotNull(result.CurrentCycle.DecisionAt);
        Assert.Equal(1, result.CurrentCycleNo);
        Assert.Equal(decision == "Approved", result.ApprovedAt.HasValue);
        Assert.Equal(decision == "Rejected", result.RejectedAt.HasValue);
        foreach (var next in new[] { "Approved", "Rejected", "ChangesRequested" })
            await Assert.ThrowsAsync<BusinessRuleException>(() => f.Decide(next));
        Assert.Equal(1, await f.Db.TravelRequestCycles.CountAsync());
        Assert.Contains(await f.Db.AuditLogs.ToListAsync(), x => x.ActionType == audit && x.ActorId == "42");
        await f.AssertTaskUnchanged();
    }

    [Fact]
    public async Task ReworkAndResubmit_PreserveIdentityFirstSubmissionAndPriorDecision()
    {
        await using var f = new Fixture();
        var first = await f.Service.SubmitAsync(1);
        await f.Decide("ChangesRequested");
        var edit = new UpdateTravelDraftDto { ApprovalRequired = true, ApproverId = "opaque-manager",
            Purpose = "Updated purpose" };
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.UpdateDraftAsync(1, edit));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.UpdateDraftAsync(1, edit, expectedCycleNo: 2));
        var edited = await f.Service.UpdateDraftAsync(1, edit, expectedCycleNo: 1);
        Assert.Equal("ChangesRequested", edited.ApprovalState);
        Assert.Equal("Updated purpose", edited.Trip.Purpose);
        Assert.Equal(1, edited.CurrentCycleNo);
        Assert.Equal("reviewer", edited.ModifiedBy);
        Assert.NotNull(edited.ModifiedDate);
        var result = await f.Service.ResubmitAsync(1, new() { ExpectedCycleNo = 1, ChangesMade = " Updated itinerary " });
        Assert.Equal(1, result.TravelRequestId);
        Assert.Equal(19, result.EaTaskId);
        Assert.Equal("TRV-TEST", result.ReferenceNo);
        Assert.Equal(first.SubmittedAt, result.SubmittedAt);
        Assert.Equal("Draft", result.BusinessState);
        Assert.Equal("Pending", result.ApprovalState);
        Assert.Equal(2, result.CurrentCycleNo);
        Assert.True(result.CurrentCycle!.SubmittedAt >= first.SubmittedAt);
        var old = await f.Db.TravelRequestCycles.SingleAsync(x => x.CycleNo == 1);
        Assert.Equal("ChangesRequested", old.DecisionState);
        Assert.Equal("Revise itinerary", old.ChangeReason);
        Assert.Equal("Updated itinerary", old.ChangesMade);
        Assert.NotNull(old.DecisionAt);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.ResubmitAsync(1, new() { ExpectedCycleNo = 1 }));
        foreach (var action in new[] { "Approved", "Rejected", "ChangesRequested" })
            await Assert.ThrowsAsync<BusinessRuleException>(() => f.Decide(action, 1));
        Assert.Equal("Approved", (await f.Decide("Approved", 2)).ApprovalState);
        Assert.Equal(2, await f.Db.TravelRequestCycles.CountAsync());
        var detail = await f.Service.GetByIdAsync(1);
        Assert.Equal(2, detail.CurrentCycle!.CycleNo);
        var item = Assert.Single((await f.Service.ListAsync(new())).Items);
        Assert.Equal(2, item.CurrentCycleNo);
        Assert.Equal(first.SubmittedAt, item.SubmittedAt);
        Assert.NotNull(item.ApprovedAt);
        await f.AssertTaskUnchanged();
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Rejected")]
    [InlineData("Approved")]
    public async Task PutAndResubmit_AreBlockedOutsideRework(string state)
    {
        await using var f = new Fixture();
        await f.Service.SubmitAsync(1);
        if (state != "Pending") await f.Decide(state);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.UpdateDraftAsync(1, new()));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.ResubmitAsync(1, new() { ExpectedCycleNo = 1 }));
    }

    [Theory]
    [InlineData(false, "opaque-manager")]
    [InlineData(true, "different")]
    public async Task Rework_RejectsRoutingChanges(bool required, string approver)
    {
        await using var f = new Fixture();
        await f.Service.SubmitAsync(1);
        await f.Decide("ChangesRequested");
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.UpdateDraftAsync(1,
            new() { ApprovalRequired = required, ApproverId = approver }, expectedCycleNo: 1));
        Assert.True(f.Parent.ApprovalRequired);
        Assert.Equal("opaque-manager", f.Parent.ApproverId);
    }

    [Fact]
    public async Task MissingAndInconsistentResources_AreNotRepaired()
    {
        await using var f = new Fixture();
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.SubmitAsync(99));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.ResubmitAsync(1, new() { ExpectedCycleNo = 1 }));
        await f.Service.SubmitAsync(1);
        var cycle = await f.Db.TravelRequestCycles.SingleAsync();
        cycle.DecisionState = "Rejected";
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Decide("Approved"));
        f.Db.TravelRequestCycles.Remove(cycle);
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => f.Decide("Approved"));
    }

    [Fact]
    public async Task MissingApproverAndOptionalText_DoNotBecomeMandatory()
    {
        await using var f = new Fixture();
        f.Parent.ApproverId = null;
        await f.Db.SaveChangesAsync();
        await f.Service.SubmitAsync(1);
        await f.Service.RequestChangesAsync(1, new() { ExpectedCycleNo = 1 });
        await f.Service.ResubmitAsync(1, new() { ExpectedCycleNo = 1 });
        var approved = await f.Service.ApproveAsync(1, new() { ExpectedCycleNo = 2 });
        Assert.Null(approved.CurrentCycle!.ApproverId);
        Assert.Null(approved.CurrentCycle.DecisionComment);
    }

    [Theory]
    [InlineData("submit")]
    [InlineData("approve")]
    [InlineData("resubmit")]
    [InlineData("put")]
    public async Task AuditFailureBeforeSave_DoesNotPersistPartialAction(string action)
    {
        await using var f = new Fixture();
        if (action != "submit" && action != "put") await f.Service.SubmitAsync(1);
        if (action == "resubmit") await f.Decide("ChangesRequested");
        var state = f.Parent.ApprovalState;
        var cycles = await f.Db.TravelRequestCycles.CountAsync();
        f.Audit.Setup(x => x.AddAudit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<object>(), It.IsAny<object>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("Audit failure"));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            switch (action)
            {
                case "submit": await f.Service.SubmitAsync(1); break;
                case "approve": await f.Decide("Approved"); break;
                case "resubmit": await f.Service.ResubmitAsync(1, new() { ExpectedCycleNo = 1 }); break;
                default: await f.Service.UpdateDraftAsync(1, new() { Purpose = "must not persist" }); break;
            }
        });
        f.Db.ChangeTracker.Clear();
        Assert.Equal(state, (await f.Db.TravelRequests.SingleAsync()).ApprovalState);
        Assert.Equal(cycles, await f.Db.TravelRequestCycles.CountAsync());
        Assert.Null((await f.Db.TravelRequests.SingleAsync()).Purpose);
    }

    [Fact]
    public void Validators_EnforceOnlyTechnicalActionConstraints()
    {
        var approve = new ApproveTravelRequestDtoValidator();
        var reject = new RejectTravelRequestDtoValidator();
        var changes = new RequestTravelChangesDtoValidator();
        var resubmit = new ResubmitTravelRequestDtoValidator();
        Assert.True(approve.Validate(new ApproveTravelRequestDto() { ExpectedCycleNo = 1 }).IsValid);
        Assert.True(reject.Validate(new RejectTravelRequestDto() { ExpectedCycleNo = 1 }).IsValid);
        Assert.True(changes.Validate(new RequestTravelChangesDto() { ExpectedCycleNo = 1 }).IsValid);
        Assert.True(resubmit.Validate(new ResubmitTravelRequestDto() { ExpectedCycleNo = 1 }).IsValid);
        Assert.False(approve.Validate(new ApproveTravelRequestDto() { ExpectedCycleNo = 0 }).IsValid);
        Assert.False(reject.Validate(new RejectTravelRequestDto() { ExpectedCycleNo = -1 }).IsValid);
        Assert.False(changes.Validate(new RequestTravelChangesDto() { ExpectedCycleNo = 1, ChangeReason = new string('x', 4001) }).IsValid);
        Assert.False(resubmit.Validate(new ResubmitTravelRequestDto() { ExpectedCycleNo = 1, ChangesMade = new string('x', 4001) }).IsValid);
        Assert.False(approve.Validate(new ApproveTravelRequestDto() { ExpectedCycleNo = 1, DecisionComment = new string('x', 4001) }).IsValid);
        Assert.False(reject.Validate(new RejectTravelRequestDto() { ExpectedCycleNo = 1, DecisionComment = new string('x', 4001) }).IsValid);
    }

    // ==================================================================
    // Step 10: Start / Complete / Cancel
    // ==================================================================

    private static async Task<Fixture> ToUpcomingAsync(bool approval)
    {
        var f = new Fixture(approval);
        await f.Service.SubmitAsync(1);
        if (approval) await f.Decide("Approved");
        return f;
    }

    private static void SeedOneChildOfEachType(Fixture f)
    {
        f.Db.TravelBookings.Add(new TravelBooking { TravelRequestId = 1, BookingType = "Flight",
            BookingStatus = "Booked", CreatedBy = "reviewer", CreatedDate = DateTime.UtcNow });
        f.Db.TravelExpenses.Add(new TravelExpense { TravelRequestId = 1, Category = "Travel", Amount = 100m,
            Status = "Approved", CreatedBy = "reviewer", CreatedDate = DateTime.UtcNow });
        f.Db.TravelHospitalityArrangements.Add(new TravelHospitality { TravelRequestId = 1,
            Status = "Scheduled", CreatedBy = "reviewer", CreatedDate = DateTime.UtcNow });
        f.Db.TravelLocalTransports.Add(new TravelLocalTransport { TravelRequestId = 1,
            TransportStatus = "Scheduled", CreatedBy = "reviewer", CreatedDate = DateTime.UtcNow });
        f.Db.Attachments.Add(new Attachment { RelatedModule = "Travel", RelatedEntity = "TravelRequest",
            RelatedEntityId = "1", OriginalFileName = "itinerary.pdf", ObjectKey = "Content/Travel/1/x.pdf",
            IsActive = true, IsDeleted = false, UploadedBy = "reviewer", UploadedAt = DateTime.UtcNow,
            CreatedBy = "reviewer", CreatedDate = DateTime.UtcNow });
        f.Db.SaveChanges();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Start_FromUpcoming_TransitionsToActive_AndAppendsAudit(bool approval)
    {
        await using var f = await ToUpcomingAsync(approval);
        var result = await f.Service.StartAsync(1);

        Assert.Equal("Active", result.BusinessState);
        Assert.Equal(approval ? "Approved" : "NotRequired", result.ApprovalState);
        Assert.NotNull(result.StartedAt);
        Assert.Equal(1, result.TravelRequestId);
        Assert.Equal("TRV-TEST", result.ReferenceNo);
        Assert.Equal(19, result.EaTaskId);

        Assert.Contains(await f.Db.AuditLogs.ToListAsync(), x => x.ActionType == "TRAVEL_START" && x.ActorId == "42");
        await f.AssertTaskUnchanged();
    }

    [Fact]
    public async Task Start_WhenApprovalRequiredButNotApproved_IsRejected()
    {
        await using var f = new Fixture(approval: true);
        await f.Service.SubmitAsync(1); // Pending — BusinessState stays Draft, not Upcoming, until Approved.
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.StartAsync(1));
    }

    [Fact]
    public async Task Start_WhenNeverSubmitted_IsRejected()
    {
        await using var f = new Fixture(approval: false);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.StartAsync(1));
    }

    [Fact]
    public async Task Start_Duplicate_IsRejected()
    {
        await using var f = await ToUpcomingAsync(false);
        await f.Service.StartAsync(1);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.StartAsync(1));
    }

    [Fact]
    public async Task GetById_ExposesStartedAt_AfterStart()
    {
        // Regression guard for the final closure check: StartedAt exists on the entity
        // (Step 10 migration) but must also round-trip through the GET detail response.
        await using var f = await ToUpcomingAsync(false);
        var started = await f.Service.StartAsync(1);
        var detail = await f.Service.GetByIdAsync(1);

        Assert.NotNull(detail.StartedAt);
        Assert.Equal(started.StartedAt, detail.StartedAt);
    }

    [Fact]
    public async Task Complete_FromActive_SetsCompletedAt_NeverOverwrites_AndLeavesApprovalAndChildrenAlone()
    {
        await using var f = await ToUpcomingAsync(true);
        SeedOneChildOfEachType(f);
        await f.Service.StartAsync(1);
        var result = await f.Service.CompleteAsync(1);

        Assert.Equal("Completed", result.BusinessState);
        Assert.Equal("Approved", result.ApprovalState);
        Assert.NotNull(result.CompletedAt);
        var firstCompletedAt = result.CompletedAt;

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.CompleteAsync(1));
        var reloaded = await f.Db.TravelRequests.AsNoTracking().SingleAsync();
        Assert.Equal(firstCompletedAt, reloaded.CompletedAt); // never overwritten by the rejected retry
        Assert.Equal("Approved", reloaded.ApprovalState);

        Assert.Equal("Booked", (await f.Db.TravelBookings.SingleAsync()).BookingStatus);
        Assert.Equal("Approved", (await f.Db.TravelExpenses.SingleAsync()).Status);
        Assert.Equal("Scheduled", (await f.Db.TravelHospitalityArrangements.SingleAsync()).Status);
        Assert.Equal("Scheduled", (await f.Db.TravelLocalTransports.SingleAsync()).TransportStatus);

        Assert.Contains(await f.Db.AuditLogs.ToListAsync(), x => x.ActionType == "TRAVEL_COMPLETE");
        await f.AssertTaskUnchanged();
    }

    [Fact]
    public async Task Complete_WhenNotActive_IsRejected()
    {
        await using var f = await ToUpcomingAsync(false);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.CompleteAsync(1));
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Upcoming")]
    [InlineData("Active")]
    public async Task Cancel_FromValidSourceState_TransitionsToCancelled_WithoutTouchingApprovalState(string source)
    {
        await using var f = new Fixture(approval: false);
        if (source != "Draft") await f.Service.SubmitAsync(1);
        if (source == "Active") await f.Service.StartAsync(1);

        var beforeApproval = f.Parent.ApprovalState;
        var result = await f.Service.CancelAsync(1);

        Assert.Equal("Cancelled", result.BusinessState);
        Assert.Equal(beforeApproval, result.ApprovalState);
        Assert.Contains(await f.Db.AuditLogs.ToListAsync(), x => x.ActionType == "TRAVEL_CANCEL");
        await f.AssertTaskUnchanged();
    }

    [Fact]
    public async Task Cancel_WhenCompleted_IsRejected()
    {
        await using var f = await ToUpcomingAsync(false);
        await f.Service.StartAsync(1);
        await f.Service.CompleteAsync(1);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.CancelAsync(1));
    }

    [Fact]
    public async Task Cancel_Duplicate_IsRejected()
    {
        await using var f = await ToUpcomingAsync(false);
        await f.Service.CancelAsync(1);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.CancelAsync(1));
    }

    [Fact]
    public async Task Cancel_PreservesApprovalHistoryDocumentsAndChildRecords_NoDeletion()
    {
        await using var f = await ToUpcomingAsync(true); // has one Approved cycle already
        SeedOneChildOfEachType(f);
        var cyclesBefore = await f.Db.TravelRequestCycles.CountAsync();

        await f.Service.CancelAsync(1);

        Assert.Equal(cyclesBefore, await f.Db.TravelRequestCycles.CountAsync());
        Assert.Equal("Approved", (await f.Db.TravelRequestCycles.SingleAsync()).DecisionState); // not rewritten

        var persisted = await f.Db.TravelRequests.AsNoTracking().SingleAsync();
        Assert.False(persisted.IsDeleted); // cancellation is a business state, never a soft-delete

        Assert.False((await f.Db.TravelBookings.SingleAsync()).IsDeleted);
        Assert.False((await f.Db.TravelExpenses.SingleAsync()).IsDeleted);
        Assert.False((await f.Db.TravelHospitalityArrangements.SingleAsync()).IsDeleted);
        Assert.False((await f.Db.TravelLocalTransports.SingleAsync()).IsDeleted);
        var doc = await f.Db.Attachments.SingleAsync();
        Assert.False(doc.IsDeleted);
        Assert.True(doc.IsActive);
    }
}
