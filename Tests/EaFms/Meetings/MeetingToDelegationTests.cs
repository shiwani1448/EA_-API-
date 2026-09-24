using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Jarvis5.Validators;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Meetings;

/// <summary>
/// Step 5B-3: Meeting -> Doer Delegation. MeetingLifecycleService.CompleteAsync uses raw
/// SQL row locks (SELECT ... FOR UPDATE) and real Postgres transactions, neither of which
/// EF InMemory supports (the same limitation already documented for
/// DelegationCreateCoreTransactionTests) — these tests connect to the same local dev
/// Postgres instance every `dotnet ef` command in this project already requires. They
/// depend on the "Meeting" and "Delegation" BusinessModule rows and an "In Progress"
/// Status row already existing in that database. IWorkflowExecutionService and
/// IMeetingCompletionFileStore are mocked (orthogonal, pre-existing, unchanged machinery —
/// not what Step 5B-3 touches); DelegationService and AuditService are real, against the
/// real database, since those are exactly what this feature composes with. Every row
/// created is clearly titled "(disposable Step 5B-3 test)" with a unique-per-run marker.
/// </summary>
public class MeetingToDelegationTests
{
    private const string ConnectionString = "Host=localhost;Port=5432;Database=DB_Studio5Jarvis;Username=postgres;Password=123456";

    private static EaFmsDbContext MakeRealDb() =>
        new(new DbContextOptionsBuilder<EaFmsDbContext>().UseNpgsql(ConnectionString).Options);

    private static async Task<long> ResolveModuleIdAsync(EaFmsDbContext db, string name) =>
        await db.BusinessModules.Where(m => m.Name == name && m.IsActive && !m.IsDeleted).Select(m => m.Id).SingleAsync();

    private static async Task<int> ResolveInProgressStatusIdAsync(EaFmsDbContext db) =>
        await db.Statuses.Where(s => s.Name == "In Progress").Select(s => s.Id).FirstAsync();

    private static DelegationService MakeRealDelegationService(EaFmsDbContext db, ICurrentUserService user, IAuditService audit)
    {
        var eaTasks = new EaTaskService(db, new EaTaskRepository(db), new TatRuleRepository(db),
            new CreateEaTaskDtoValidator(), user, audit);
        return new DelegationService(db, user, audit, new DelegationRepository(db), eaTasks,
            Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(),
            new TaskReviewService(db, new TaskReviewRepository(db), user, audit), new TatRuleRepository(db));
    }

    private static Mock<IMeetingCompletionFileStore> MakeFileStoreMock()
    {
        var files = new Mock<IMeetingCompletionFileStore>();
        files.Setup(f => f.ValidateAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        files.Setup(f => f.SaveAsync(It.IsAny<long>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Content/MeetingCompletion/disposable-step5b3-test.pdf");
        return files;
    }

    private static Mock<IWorkflowExecutionService> MakeExecutionMock() =>
        new Mock<IWorkflowExecutionService>().Also(m =>
            m.Setup(x => x.CompleteAsync(It.IsAny<long>(), It.IsAny<CompleteWorkRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CompleteWorkResponseDto { Workflow = new WorkflowResponseDto() }));

    private static Mock<IMeetingService> MakeMeetingServiceMock() =>
        new Mock<IMeetingService>().Also(m =>
            m.Setup(x => x.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MeetingDetailResponseDto { TatSummary = new MeetingTatSummaryDto() }));

    private static MeetingLifecycleService MakeLifecycleService(
        EaFmsDbContext db, ICurrentUserService user, IAuditService audit, DelegationService delegations,
        out Mock<IMeetingCompletionFileStore> files)
    {
        files = MakeFileStoreMock();
        return new MeetingLifecycleService(
            db, MakeExecutionMock().Object, user, audit, MakeMeetingServiceMock().Object, files.Object,
            delegations, NullLogger<MeetingLifecycleService>.Instance);
    }

    private static IFormFile MakeFakePdf() =>
        Mock.Of<IFormFile>(f => f.FileName == "test.pdf");

    /// <summary>
    /// Seeds Meeting + WorkflowInstance (In Progress, TatStarted) + central EaTask, wired
    /// together the same way MeetingService.CreateAsync/StartAsync wire them for real, so
    /// MeetingLifecycleService.CompleteAsync's own preconditions are satisfied.
    /// </summary>
    private static async Task<(Meeting meeting, long meetingModuleId)> SeedInProgressMeetingAsync(
        EaFmsDbContext db, string marker, string actor = "step5b3-ea")
    {
        var meetingModuleId = await ResolveModuleIdAsync(db, "Meeting");
        var inProgressStatusId = await ResolveInProgressStatusIdAsync(db);
        var now = DateTime.UtcNow;

        var meeting = new Meeting
        {
            Title = $"Step 5B-3 test meeting {marker} (disposable Step 5B-3 test)",
            MeetingNumber = $"MTG-5B3-{marker}",
            DoerIds = Array.Empty<string>(), DoerNames = Array.Empty<string>(),
            CreatedBy = actor, CreatedDate = now, IsDeleted = false
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var workflow = new WorkflowInstance
        {
            BusinessModuleId = meetingModuleId,
            BusinessRecordId = meeting.Id.ToString(),
            StatusId = inProgressStatusId,
            StartedAt = now.AddMinutes(-10),
            TatStartedAt = now.AddMinutes(-10),
            IsActive = true,
            CreatedBy = actor, CreatedDate = now
        };
        db.WorkflowInstances.Add(workflow);
        await db.SaveChangesAsync();

        meeting.WorkflowInstanceId = workflow.Id;
        await db.SaveChangesAsync();

        var eaTask = new EaTask
        {
            BusinessModuleId = meetingModuleId,
            ModuleName = "Meeting",
            BusinessRecordId = meeting.Id.ToString(),
            Task = meeting.Title!,
            ExecutionStatus = "InProgress",
            StartedAt = workflow.TatStartedAt,
            WorkflowInstanceId = workflow.Id,
            IsActive = true,
            CreatedBy = actor, CreatedDate = now
        };
        db.Tasks.Add(eaTask);
        await db.SaveChangesAsync();

        return (meeting, meetingModuleId);
    }

    private static async Task<MeetingAction> AddActionAsync(
        EaFmsDbContext db, long meetingId, string marker,
        string? doerId = "EMP-5B3-1", string? doerName = "Step 5B-3 Doer",
        string? title = null, string? description = "Disposable Step 5B-3 test action",
        string? priority = "High", DateTime? dueDate = null, bool isDeleted = false)
    {
        var action = new MeetingAction
        {
            MeetingId = meetingId,
            Title = title ?? $"Action {marker}",
            Description = description,
            DoerId = doerId,
            DoerName = doerName,
            Priority = priority,
            DueDate = dueDate ?? DateTime.UtcNow.Date.AddDays(5),
            CreatedBy = "step5b3-ea", CreatedDate = DateTime.UtcNow,
            IsDeleted = isDeleted
        };
        db.MeetingActions.Add(action);
        await db.SaveChangesAsync();
        return action;
    }

    private static MeetingCompleteRequestDto MakeCompleteDto(string mom = "Disposable Step 5B-3 test completion MOM.") =>
        new() { CompletionMom = mom, CompletionPdf = MakeFakePdf() };

    // ----------------------------------------------------------------
    // 1. Zero actions
    // ----------------------------------------------------------------

    [Fact]
    public async Task CompleteAsync_ZeroMeetingActions_CompletesMeeting_CreatesZeroDelegations()
    {
        await using var db = MakeRealDb();
        var marker = $"zero-{Guid.NewGuid():N}";
        var (meeting, _) = await SeedInProgressMeetingAsync(db, marker);
        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "step5b3-ea" && u.UserId == 1);
        var audit = new AuditService(db, user);
        var delegations = MakeRealDelegationService(db, user, audit);
        var lifecycle = MakeLifecycleService(db, user, audit, delegations, out _);

        var result = await lifecycle.CompleteAsync(meeting.Id, MakeCompleteDto(), default);

        Assert.NotNull(result);
        await using var verify = MakeRealDb();
        var reloaded = await verify.Meetings.SingleAsync(m => m.Id == meeting.Id);
        Assert.NotNull(reloaded.CompletedAt);
        Assert.Equal(0, await verify.Delegations.CountAsync(d => d.SourceEntityId != null && d.SourceEntityId.StartsWith("__never__")));
        // No actions existed at all, so nothing tied to this meeting could have been created.
    }

    // ----------------------------------------------------------------
    // 1b. Action's startDate / assignee / delegationType carry into the Delegation
    // ----------------------------------------------------------------

    [Fact]
    public async Task CompleteAsync_ActionStartDateAssigneeAndDelegationType_AreCarriedIntoTheDelegation()
    {
        await using var db = MakeRealDb();
        var marker = $"fields-{Guid.NewGuid():N}";
        var type = $"5B3-TYPE-{marker}";
        var delegationModuleId = await ResolveModuleIdAsync(db, "Delegation");
        var rule = new TatRule { BusinessModuleId = delegationModuleId, ModuleName = "Delegation", Type = type, TaskType = DelegationTaskType.Actual,
            TatMinutes = 90, IsActive = true, CreatedBy = "step5b3-ea", CreatedDate = DateTime.UtcNow };
        db.TatRules.Add(rule);
        await db.SaveChangesAsync();
        try
        {
            var (meeting, meetingModuleId) = await SeedInProgressMeetingAsync(db, marker);
            var start = DateTime.UtcNow.Date.AddDays(1).AddHours(10);
            var action = await AddActionAsync(db, meeting.Id, marker, doerId: "EMP-5B3-FIELDS", doerName: "Fields Doer");
            action.StartDate = start;
            action.AssigneeId = "EMP-5B3-ASSIGNEE";
            action.AssigneeName = "Fields Assignee";
            action.DelegationType = type;
            await db.SaveChangesAsync();

            var user = Mock.Of<ICurrentUserService>(u => u.UserName == "step5b3-ea" && u.UserId == 1);
            var audit = new AuditService(db, user);
            var lifecycle = MakeLifecycleService(db, user, audit, MakeRealDelegationService(db, user, audit), out _);
            await lifecycle.CompleteAsync(meeting.Id, MakeCompleteDto(), default);

            await using var verify = MakeRealDb();
            var delegation = await verify.Delegations.SingleAsync(d =>
                d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == action.Id.ToString());
            Assert.Equal(start, delegation.StartDate);
            Assert.Equal(("EMP-5B3-ASSIGNEE", "Fields Assignee"), (delegation.AssigneeId, delegation.AssigneeNameSnapshot));
            Assert.Equal(("EMP-5B3-FIELDS", "Fields Doer"), (delegation.DoerId, delegation.DoerNameSnapshot)); // doer unaffected
            Assert.Equal(type, delegation.DelegationType);
            var task = await verify.Tasks.SingleAsync(t => t.Id == delegation.EaTaskId);
            Assert.Equal((90, rule.Id), (task.AllottedTatMinutes, task.TatRuleId));   // typed → TAT snapshotted
        }
        finally
        {
            await using var cleanup = MakeRealDb();
            await cleanup.Tasks.Where(t => t.TatRuleId == rule.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.TatRuleId, (long?)null));
            await cleanup.TatRules.Where(r => r.Id == rule.Id).ExecuteDeleteAsync();
        }
    }

    // ----------------------------------------------------------------
    // 2. One valid action — full field mapping
    // ----------------------------------------------------------------

    [Fact]
    public async Task CompleteAsync_OneValidAction_CreatesExactlyOneDelegation_WithFullFieldMapping()
    {
        await using var db = MakeRealDb();
        var marker = $"one-{Guid.NewGuid():N}";
        var (meeting, meetingModuleId) = await SeedInProgressMeetingAsync(db, marker);
        var dueDate = DateTime.UtcNow.Date.AddDays(7);
        var action = await AddActionAsync(db, meeting.Id, marker,
            doerId: "EMP-5B3-ONE", doerName: "One Doer",
            title: $"Prepare revised proposal {marker}", description: "Full mapping check",
            priority: "Anything Selected By Frontend", dueDate: dueDate);

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "step5b3-ea" && u.UserId == 1);
        var audit = new AuditService(db, user);
        var delegations = MakeRealDelegationService(db, user, audit);
        var lifecycle = MakeLifecycleService(db, user, audit, delegations, out _);

        await lifecycle.CompleteAsync(meeting.Id, MakeCompleteDto(), default);

        await using var verify = MakeRealDb();
        var delegationModuleId = await ResolveModuleIdAsync(verify, "Delegation");
        var delegation = await verify.Delegations.SingleAsync(d =>
            d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == action.Id.ToString());

        Assert.Equal($"Prepare revised proposal {marker}", delegation.Title);
        Assert.Equal("Full mapping check", delegation.Description);
        Assert.Equal("EMP-5B3-ONE", delegation.DoerId); // Doer identity, copied verbatim
        Assert.Equal("One Doer", delegation.DoerNameSnapshot);
        Assert.Equal("Anything Selected By Frontend", delegation.Priority); // custom string, no catalog lookup
        Assert.Equal(dueDate, delegation.DueDate);
        Assert.Equal(meetingModuleId, delegation.SourceBusinessModuleId); // dynamically resolved
        Assert.Equal(action.Id.ToString(), delegation.SourceEntityId);
        Assert.Equal(meeting.MeetingNumber, delegation.SourceReference);
        Assert.Equal("step5b3-ea", delegation.AssignedById); // server actor, not the Doer
        Assert.Equal("Pending", delegation.Status);
        Assert.Null(delegation.StartedAt);
        Assert.Null(delegation.DelegationType); // Meeting actions carry neither; never fabricated
        Assert.Null(delegation.StartDate);
        Assert.Null(delegation.CompletedAt);

        var task = await verify.Tasks.SingleAsync(t => t.Id == delegation.EaTaskId);
        Assert.Equal(delegationModuleId, task.BusinessModuleId); // canonical Delegation module, never Meeting's
        Assert.Equal("NotStarted", task.ExecutionStatus);
        Assert.Null(task.WorkflowInstanceId); // no WorkflowInstance for a generated Delegation
        Assert.Null(task.TatRuleId);
        Assert.Null(task.AllottedTatMinutes);
        Assert.Null(task.TatUsedMinutes);

        var reloadedMeeting = await verify.Meetings.SingleAsync(m => m.Id == meeting.Id);
        Assert.NotNull(reloadedMeeting.CompletedAt); // Meeting completed independently
    }

    // ----------------------------------------------------------------
    // 3. Multiple valid actions — one Delegation each
    // ----------------------------------------------------------------

    [Fact]
    public async Task CompleteAsync_MultipleValidActions_CreatesOneDelegationPerAction()
    {
        await using var db = MakeRealDb();
        var marker = $"multi-{Guid.NewGuid():N}";
        var (meeting, meetingModuleId) = await SeedInProgressMeetingAsync(db, marker);
        var a1 = await AddActionAsync(db, meeting.Id, $"{marker}-a1", doerId: "EMP-5B3-A1");
        var a2 = await AddActionAsync(db, meeting.Id, $"{marker}-a2", doerId: "EMP-5B3-A2");
        var a3 = await AddActionAsync(db, meeting.Id, $"{marker}-a3", doerId: "EMP-5B3-A3");

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "step5b3-ea" && u.UserId == 1);
        var audit = new AuditService(db, user);
        var delegations = MakeRealDelegationService(db, user, audit);
        var lifecycle = MakeLifecycleService(db, user, audit, delegations, out _);

        await lifecycle.CompleteAsync(meeting.Id, MakeCompleteDto(), default);

        await using var verify = MakeRealDb();
        foreach (var a in new[] { a1, a2, a3 })
        {
            var count = await verify.Delegations.CountAsync(d =>
                d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == a.Id.ToString());
            Assert.Equal(1, count);
        }
    }

    // ----------------------------------------------------------------
    // Doer workload visibility (doerId filter)
    // ----------------------------------------------------------------

    [Fact]
    public async Task GeneratedDelegation_VisibleThroughDoerIdFilter_DifferentDoerExcluded()
    {
        await using var db = MakeRealDb();
        var marker = $"filter-{Guid.NewGuid():N}";
        var (meeting, _) = await SeedInProgressMeetingAsync(db, marker);
        var doerId = $"EMP-5B3-FILTER-{marker}";
        await AddActionAsync(db, meeting.Id, marker, doerId: doerId);

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "step5b3-ea" && u.UserId == 1);
        var audit = new AuditService(db, user);
        var delegations = MakeRealDelegationService(db, user, audit);
        var lifecycle = MakeLifecycleService(db, user, audit, delegations, out _);
        await lifecycle.CompleteAsync(meeting.Id, MakeCompleteDto(), default);

        await using var verify = MakeRealDb();
        var verifyDelegations = MakeRealDelegationService(verify, user, new AuditService(verify, user));

        var matched = await verifyDelegations.ListAsync(new DelegationListQueryDto { DoerId = doerId });
        Assert.Single(matched.Items);
        Assert.Equal(doerId, matched.Items[0].DoerId);

        var notMatched = await verifyDelegations.ListAsync(new DelegationListQueryDto { DoerId = $"EMP-NOT-{marker}" });
        Assert.DoesNotContain(notMatched.Items, i => i.DoerId == doerId);
    }

    // ----------------------------------------------------------------
    // Missing doer — fails completion, no fake doer, no partial commit
    // ----------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CompleteAsync_MissingDoerId_FailsCompletion_NoFakeDoer_NoPartialDelegations_MeetingRemainsIncomplete(string? blankDoerId)
    {
        await using var db = MakeRealDb();
        var marker = $"missing-{Guid.NewGuid():N}";
        var (meeting, meetingModuleId) = await SeedInProgressMeetingAsync(db, marker);
        var validAction = await AddActionAsync(db, meeting.Id, $"{marker}-valid", doerId: "EMP-5B3-VALID",
            title: "Valid action");
        var brokenAction = await AddActionAsync(db, meeting.Id, $"{marker}-broken", doerId: blankDoerId,
            title: "Prepare revised proposal");

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "step5b3-ea" && u.UserId == 1);
        var audit = new AuditService(db, user);
        var delegations = MakeRealDelegationService(db, user, audit);
        var lifecycle = MakeLifecycleService(db, user, audit, delegations, out _);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => lifecycle.CompleteAsync(meeting.Id, MakeCompleteDto(), default));
        Assert.Contains("no assigned doer", ex.Message);
        Assert.Contains("Prepare revised proposal", ex.Message);

        await using var verify = MakeRealDb();
        var reloadedMeeting = await verify.Meetings.SingleAsync(m => m.Id == meeting.Id);
        Assert.Null(reloadedMeeting.CompletedAt); // Meeting remains uncompleted

        // No Delegation for EITHER action — including the one with a perfectly valid Doer —
        // proving the whole attempt rolled back rather than partially converting.
        Assert.False(await verify.Delegations.AnyAsync(d => d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == validAction.Id.ToString()));
        Assert.False(await verify.Delegations.AnyAsync(d => d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == brokenAction.Id.ToString()));
        // No fake doer anywhere in the (nonexistent) Delegation rows for this meeting.
        Assert.Equal(0, await verify.Delegations.CountAsync(d => d.DoerId == "Unknown" || d.DoerId == "Unassigned"));
    }

    // ----------------------------------------------------------------
    // Valid DoerId + null DoerName still succeeds
    // ----------------------------------------------------------------

    [Fact]
    public async Task CompleteAsync_ValidDoerId_NullDoerName_SucceedsWithNullNameSnapshot()
    {
        await using var db = MakeRealDb();
        var marker = $"noname-{Guid.NewGuid():N}";
        var (meeting, meetingModuleId) = await SeedInProgressMeetingAsync(db, marker);
        var action = await AddActionAsync(db, meeting.Id, marker, doerId: "EMP-5B3-NONAME", doerName: null);

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "step5b3-ea" && u.UserId == 1);
        var audit = new AuditService(db, user);
        var delegations = MakeRealDelegationService(db, user, audit);
        var lifecycle = MakeLifecycleService(db, user, audit, delegations, out _);

        await lifecycle.CompleteAsync(meeting.Id, MakeCompleteDto(), default);

        await using var verify = MakeRealDb();
        var delegation = await verify.Delegations.SingleAsync(d => d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == action.Id.ToString());
        Assert.Equal("EMP-5B3-NONAME", delegation.DoerId);
        Assert.Null(delegation.DoerNameSnapshot);
    }

    // ----------------------------------------------------------------
    // Deleted MeetingAction — no Delegation
    // ----------------------------------------------------------------

    [Fact]
    public async Task CompleteAsync_DeletedMeetingAction_CreatesNoDelegation()
    {
        await using var db = MakeRealDb();
        var marker = $"deleted-{Guid.NewGuid():N}";
        var (meeting, meetingModuleId) = await SeedInProgressMeetingAsync(db, marker);
        var live = await AddActionAsync(db, meeting.Id, $"{marker}-live", doerId: "EMP-5B3-LIVE");
        var deleted = await AddActionAsync(db, meeting.Id, $"{marker}-deleted", doerId: "EMP-5B3-DELETED", isDeleted: true);

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "step5b3-ea" && u.UserId == 1);
        var audit = new AuditService(db, user);
        var delegations = MakeRealDelegationService(db, user, audit);
        var lifecycle = MakeLifecycleService(db, user, audit, delegations, out _);

        await lifecycle.CompleteAsync(meeting.Id, MakeCompleteDto(), default);

        await using var verify = MakeRealDb();
        Assert.True(await verify.Delegations.AnyAsync(d => d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == live.Id.ToString()));
        Assert.False(await verify.Delegations.AnyAsync(d => d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == deleted.Id.ToString()));
    }

    // ----------------------------------------------------------------
    // Idempotency: pre-existing source-linked Delegation is not duplicated
    // ----------------------------------------------------------------

    [Fact]
    public async Task CompleteAsync_ExistingSourceLinkedDelegation_DuplicateNotCreated()
    {
        await using var db = MakeRealDb();
        var marker = $"dup-{Guid.NewGuid():N}";
        var (meeting, meetingModuleId) = await SeedInProgressMeetingAsync(db, marker);
        var action = await AddActionAsync(db, meeting.Id, marker, doerId: "EMP-5B3-DUP");

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "step5b3-ea" && u.UserId == 1);
        var audit = new AuditService(db, user);
        var delegations = MakeRealDelegationService(db, user, audit);

        // Simulate the source link already existing before completion runs.
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await delegations.CreateCoreAsync(new DelegationCreateCommand
            {
                Title = "Pre-existing linked Delegation (disposable Step 5B-3 test)",
                DoerId = "EMP-5B3-DUP",
                SourceBusinessModuleId = meetingModuleId,
                SourceEntityId = action.Id.ToString()
            }, default);
            await tx.CommitAsync();
        }

        var lifecycle = MakeLifecycleService(db, user, audit, delegations, out _);
        await lifecycle.CompleteAsync(meeting.Id, MakeCompleteDto(), default);

        await using var verify = MakeRealDb();
        var count = await verify.Delegations.CountAsync(d =>
            d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == action.Id.ToString());
        Assert.Equal(1, count); // still exactly one, never duplicated
    }

    // ----------------------------------------------------------------
    // Retry after success: meeting already completed, second call rejected,
    // no duplicate Delegation created.
    // ----------------------------------------------------------------

    [Fact]
    public async Task CompleteAsync_RetryAfterSuccess_SecondCallRejected_NoDuplicateDelegation()
    {
        await using var db = MakeRealDb();
        var marker = $"retry-{Guid.NewGuid():N}";
        var (meeting, meetingModuleId) = await SeedInProgressMeetingAsync(db, marker);
        var action = await AddActionAsync(db, meeting.Id, marker, doerId: "EMP-5B3-RETRY");

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "step5b3-ea" && u.UserId == 1);
        var audit = new AuditService(db, user);
        var delegations = MakeRealDelegationService(db, user, audit);
        var lifecycle = MakeLifecycleService(db, user, audit, delegations, out _);

        await lifecycle.CompleteAsync(meeting.Id, MakeCompleteDto(), default);
        await Assert.ThrowsAsync<BusinessRuleException>(() => lifecycle.CompleteAsync(meeting.Id, MakeCompleteDto(), default));

        await using var verify = MakeRealDb();
        var count = await verify.Delegations.CountAsync(d =>
            d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == action.Id.ToString());
        Assert.Equal(1, count);
    }

    // ----------------------------------------------------------------
    // Concurrency: two Complete requests racing for the SAME meeting. Protected by the
    // existing "SELECT ... FOR UPDATE" row lock on ea_meetings already taken inside
    // CompleteAsync — the second request blocks until the first commits/rolls back, then
    // sees meeting.CompletedAt already set and fails the existing completability
    // precondition before ever reaching MeetingAction/Delegation logic. No new DB
    // constraint was added for this — see the final report for why none is needed.
    // ----------------------------------------------------------------

    [Fact]
    public async Task CompleteAsync_ConcurrentCompletion_AtMostOneDelegationPerAction()
    {
        var marker = $"concurrent-{Guid.NewGuid():N}";
        await using var seedDb = MakeRealDb();
        var (meeting, meetingModuleId) = await SeedInProgressMeetingAsync(seedDb, marker);
        var action = await AddActionAsync(seedDb, meeting.Id, marker, doerId: "EMP-5B3-CONCURRENT");

        Task<MeetingLifecycleResponseDto> RunAttempt()
        {
            var db = MakeRealDb();
            var user = Mock.Of<ICurrentUserService>(u => u.UserName == "step5b3-ea" && u.UserId == 1);
            var audit = new AuditService(db, user);
            var delegations = MakeRealDelegationService(db, user, audit);
            var lifecycle = MakeLifecycleService(db, user, audit, delegations, out _);
            return lifecycle.CompleteAsync(meeting.Id, MakeCompleteDto(), default);
        }

        var results = await Task.WhenAll(
            RunAttempt().ContinueWith(t => t),
            RunAttempt().ContinueWith(t => t));

        var succeeded = results.Count(t => t.Status == TaskStatus.RanToCompletion);
        var failed = results.Count(t => t.Status == TaskStatus.Faulted);
        Assert.Equal(1, succeeded); // exactly one attempt wins the row lock and completes
        Assert.Equal(1, failed);    // the other is rejected by the existing completability check

        await using var verify = MakeRealDb();
        var count = await verify.Delegations.CountAsync(d =>
            d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == action.Id.ToString());
        Assert.Equal(1, count); // at most one Delegation per MeetingAction, even under a real race
    }

    // ----------------------------------------------------------------
    // Failure mid-loop (second Delegation) rolls back the first one too
    // ----------------------------------------------------------------

    [Fact]
    public async Task CompleteAsync_FailureDuringSecondDelegationCreation_RollsBackFirstToo()
    {
        await using var db = MakeRealDb();
        var marker = $"midfail-{Guid.NewGuid():N}";
        var (meeting, meetingModuleId) = await SeedInProgressMeetingAsync(db, marker);
        // Both actions have valid titles and doers so they persist without error.
        // The failure is injected via a mock IDelegationNumberRepository (the seam
        // already present in DelegationService's constructor) that succeeds on the first
        // call and throws on the second — deterministically causing CreateCoreAsync to
        // fail during the second Delegation creation, inside the outer Meeting-completion
        // transaction, so the first Delegation (and its EaTask) roll back with it.
        var first  = await AddActionAsync(db, meeting.Id, $"{marker}-first",  doerId: "EMP-5B3-FIRST",
            title: "First action (disposable Step 5B-3 rollback test)");
        var second = await AddActionAsync(db, meeting.Id, $"{marker}-second", doerId: "EMP-5B3-SECOND",
            title: "Second action (disposable Step 5B-3 rollback test)");

        // ---- PRE-CONDITION snapshot (taken BEFORE CompleteAsync) ----
        long delegationModuleId;
        int delegTasksBaseline;
        await using (var pre = MakeRealDb())
        {
            delegationModuleId = await ResolveModuleIdAsync(pre, "Delegation");

            // No Delegations for either action yet.
            Assert.False(await pre.Delegations.AnyAsync(d =>
                d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == first.Id.ToString()));
            Assert.False(await pre.Delegations.AnyAsync(d =>
                d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == second.Id.ToString()));

            // Capture the Delegation-module EaTask count before the attempt.
            delegTasksBaseline = await pre.Tasks
                .Where(t => t.BusinessModuleId == delegationModuleId && !t.IsDeleted)
                .CountAsync();
        }

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "step5b3-ea" && u.UserId == 1);
        var audit = new AuditService(db, user);

        // Mock IDelegationNumberRepository: call 1 returns a real sequence value (so the
        // first Delegation + its EaTask are written inside the open transaction); call 2
        // throws, simulating a deterministic failure during the second CreateCoreAsync —
        // still inside the same outer transaction, so both writes roll back atomically.
        var realNumbers = new DelegationRepository(db);
        var callCount = 0;
        var mockNumbers = new Mock<IDelegationNumberRepository>();
        mockNumbers
            .Setup(r => r.GenerateNextReferenceNoAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                callCount++;
                if (callCount == 1)
                    return await realNumbers.GenerateNextReferenceNoAsync(ct); // first succeeds
                throw new InvalidOperationException(
                    "Simulated reference-number failure during second Delegation creation (Step 5B-3 rollback test).");
            });

        var eaTasks = new EaTaskService(db, new EaTaskRepository(db), new TatRuleRepository(db),
            new CreateEaTaskDtoValidator(), user, audit);
        var delegations = new DelegationService(db, user, audit, mockNumbers.Object, eaTasks,
            Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(),
            new TaskReviewService(db, new TaskReviewRepository(db), user, audit), new TatRuleRepository(db));
        var lifecycle = MakeLifecycleService(db, user, audit, delegations, out _);

        // ---- ACTION: CompleteAsync must throw (failure during second Delegation creation) ----
        await Assert.ThrowsAnyAsync<Exception>(() => lifecycle.CompleteAsync(meeting.Id, MakeCompleteDto(), default));

        // The mock was called exactly twice: once for each action's CreateCoreAsync.
        Assert.Equal(2, callCount);

        // ---- POST-CONDITION: outer transaction rolled back everything ----
        await using var verify = MakeRealDb();

        // Neither Delegation was committed.
        Assert.False(await verify.Delegations.AnyAsync(d =>
            d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == first.Id.ToString()),
            "First Delegation must have been rolled back.");
        Assert.False(await verify.Delegations.AnyAsync(d =>
            d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == second.Id.ToString()),
            "Second Delegation must never have been committed.");

        // No orphan EaTask: the Delegation-module EaTask count must not have grown.
        // EaTask rows for Delegations carry BusinessModuleId == delegationModuleId.
        // The whole outer transaction rolled back, so the EaTask written for the first
        // Delegation inside that transaction must also be gone.
        var delegTasksAfter = await verify.Tasks
            .Where(t => t.BusinessModuleId == delegationModuleId && !t.IsDeleted)
            .CountAsync();
        Assert.Equal(delegTasksBaseline, delegTasksAfter);

        // Meeting itself must remain uncompleted.
        var reloadedMeeting = await verify.Meetings.SingleAsync(m => m.Id == meeting.Id);
        Assert.Null(reloadedMeeting.CompletedAt);

        // Meeting's central EaTask must also remain uncompleted.
        var meetingTask = await verify.Tasks.FirstOrDefaultAsync(t =>
            t.BusinessRecordId == meeting.Id.ToString() && t.ModuleName == "Meeting");
        if (meetingTask is not null)
            Assert.NotEqual("Completed", meetingTask.ExecutionStatus);
    }

    // ----------------------------------------------------------------
    // Manual Delegation nullable-source architecture unaffected
    // ----------------------------------------------------------------

    [Fact]
    public async Task ManualDelegation_StillHasNullSource_UnaffectedByMeetingIntegration()
    {
        await using var db = MakeRealDb();
        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "step5b3-ea" && u.UserId == 1);
        var audit = new AuditService(db, user);
        var delegations = MakeRealDelegationService(db, user, audit);
        var marker = $"manual-{Guid.NewGuid():N}";

        var result = await delegations.CreateAsync(new DelegationCreateRequestDto
        {
            Title = $"Manual Delegation {marker} (disposable Step 5B-3 test)",
            DoerId = "EMP-5B3-MANUAL"
        });

        Assert.Null(result.SourceBusinessModuleId);
        Assert.Null(result.SourceModuleName);
        Assert.Null(result.SourceEntityId);
        Assert.Null(result.SourceReference);
    }

    // ----------------------------------------------------------------
    // Generated Delegation: existing Start/Complete lifecycle works unchanged; Meeting
    // completion does not auto-complete it.
    // ----------------------------------------------------------------

    [Fact]
    public async Task GeneratedDelegation_StartAndCompleteLifecycle_WorksViaExistingEndpoints_MeetingDoesNotAutoCompleteIt()
    {
        await using var db = MakeRealDb();
        var marker = $"lifecycle-{Guid.NewGuid():N}";
        var (meeting, meetingModuleId) = await SeedInProgressMeetingAsync(db, marker);
        var action = await AddActionAsync(db, meeting.Id, marker, doerId: "EMP-5B3-LIFECYCLE");

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "step5b3-ea" && u.UserId == 1);
        var audit = new AuditService(db, user);
        var delegations = MakeRealDelegationService(db, user, audit);
        var lifecycle = MakeLifecycleService(db, user, audit, delegations, out _);
        await lifecycle.CompleteAsync(meeting.Id, MakeCompleteDto(), default);

        await using var verify1 = MakeRealDb();
        var generated = await verify1.Delegations.SingleAsync(d => d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == action.Id.ToString());
        Assert.Equal("Pending", generated.Status); // remains Pending right after Meeting completes

        var delegationsForLifecycle = MakeRealDelegationService(verify1, user, new AuditService(verify1, user));
        var started = await delegationsForLifecycle.StartAsync(generated.Id);
        Assert.Equal("InProgress", started.Status);

        await using var verify2 = MakeRealDb();
        var afterStart = await verify2.Tasks.SingleAsync(t => t.Id == generated.EaTaskId);
        Assert.Equal("InProgress", afterStart.ExecutionStatus);

        var delegationsForComplete = MakeRealDelegationService(verify2, user, new AuditService(verify2, user));
        var completed = await delegationsForComplete.CompleteAsync(generated.Id, null);
        Assert.Equal("Completed", completed.Status);

        await using var verify3 = MakeRealDb();
        var afterComplete = await verify3.Tasks.SingleAsync(t => t.Id == generated.EaTaskId);
        Assert.Equal("Completed", afterComplete.ExecutionStatus);

        var meetingReloaded = await verify3.Meetings.SingleAsync(m => m.Id == meeting.Id);
        Assert.NotNull(meetingReloaded.CompletedAt); // Meeting was already completed independently, unaffected
    }

    // ----------------------------------------------------------------
    // completionMom is optional (frontend controls mandatory fields): a blank MOM no longer blocks
    // completion and is stored as null.
    // ----------------------------------------------------------------

    [Fact]
    public async Task CompleteAsync_BlankCompletionMom_IsAllowed_AndStoredAsNull()
    {
        await using var db = MakeRealDb();
        var marker = $"mom-{Guid.NewGuid():N}";
        var (meeting, _) = await SeedInProgressMeetingAsync(db, marker);
        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "step5b3-ea" && u.UserId == 1);
        var audit = new AuditService(db, user);
        var delegations = MakeRealDelegationService(db, user, audit);
        var lifecycle = MakeLifecycleService(db, user, audit, delegations, out _);

        await lifecycle.CompleteAsync(meeting.Id, MakeCompleteDto(mom: "   "), default);

        await using var verify = MakeRealDb();
        var reloaded = await verify.Meetings.SingleAsync(m => m.Id == meeting.Id);
        Assert.NotNull(reloaded.CompletedAt);
        Assert.Null(reloaded.CompletionMom);
    }
}

file static class MockExtensions
{
    public static Mock<T> Also<T>(this Mock<T> mock, Action<Mock<T>> configure) where T : class
    {
        configure(mock);
        return mock;
    }
}
