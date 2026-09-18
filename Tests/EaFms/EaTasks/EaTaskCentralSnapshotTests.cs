using System;
using System.Linq;
using System.Threading.Tasks;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Jarvis5.Validators;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.EaTasks;

/// <summary>
/// EaTaskService.CreateCoreAsync always runs Postgres-only raw SQL (FOR SHARE row locks,
/// advisory locks) before it ever touches the new snapshot fields, so — same structural
/// limitation as EaTaskNoTatAuthorizationTests and every Travel/Approval/Delegation test
/// in this suite — it cannot be exercised end-to-end against EF InMemory. These tests
/// instead cover what IS testable without a real Postgres instance:
///   1. the entity/EF configuration for the new columns round-trips correctly,
///   2. the extracted CalculateTatDifferenceMinutes pure function and the canonical
///      MeetingExecutionStateMapper/EaTaskExecutionStatus helpers,
///   3. the real (non-mocked) EaTaskService.GetAsync/QueryAsync/GetHistoryAsync read
///      paths, which use no raw SQL and so ARE fully exercisable — including derived
///      ExecutionStatus/CurrentTatUsedMinutes/IsPaused/PauseCount and the central
///      execution timeline built from WorkflowHistory/WorkPause/AuditLog.
/// The write-path assignment inside CreateCoreAsync/MeetingLifecycleService/
/// TravelRequestService.Lifecycle/ApprovalService/ApprovalLifecycleService was verified
/// by direct code inspection; see the accompanying report for the reasoning.
/// </summary>
public class EaTaskCentralSnapshotTests
{
    private static EaFmsDbContext MakeDb() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private static async Task<BusinessModule> AddModuleAsync(EaFmsDbContext db, string name)
    {
        var m = new BusinessModule { Name = name, IsActive = true, IsDeleted = false, CreatedBy = "tester", CreatedDate = DateTime.UtcNow };
        db.BusinessModules.Add(m);
        await db.SaveChangesAsync();
        return m;
    }

    private static EaTaskService MakeService(EaFmsDbContext db) => new(
        db, new EaTaskRepository(db), new TatRuleRepository(db), new CreateEaTaskDtoValidator(),
        Mock.Of<ICurrentUserService>(u => u.UserId == 1), Mock.Of<IAuditService>());

    [Fact]
    public async Task EaTask_TatEnabledSnapshot_PersistsAndRoundTrips_IncludingTatRuleNavigation()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, "Meeting");
        var rule = new TatRule { BusinessModuleId = module.Id, ModuleName = "Meeting", Type = "Internal", Subtype = "Review",
            TatMinutes = 120, IsActive = true, CreatedBy = "tester", CreatedDate = DateTime.UtcNow };
        db.TatRules.Add(rule);
        await db.SaveChangesAsync();

        var task = new EaTask
        {
            BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = "1",
            Task = "Client Review", Type = "Internal", Subtype = "Review",
            TatRuleId = rule.Id, AllottedTatMinutes = 120, TatUsedMinutes = 90,
            ExecutionStatus = EaTaskExecutionStatus.Completed,
            StartedAt = DateTime.UtcNow.AddHours(-2), CompletedAt = DateTime.UtcNow.AddMinutes(-30),
            IsActive = true, CreatedBy = "tester", CreatedDate = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var reloaded = await db.Tasks.AsNoTracking().Include(t => t.TatRule).SingleAsync(t => t.Id == task.Id);

        Assert.Equal("Meeting", reloaded.ModuleName);
        Assert.Equal("Internal", reloaded.Type);
        Assert.Equal("Review", reloaded.Subtype);
        Assert.Equal(rule.Id, reloaded.TatRuleId);
        Assert.Equal(120, reloaded.AllottedTatMinutes);
        Assert.Equal(90, reloaded.TatUsedMinutes);
        Assert.Equal(EaTaskExecutionStatus.Completed, reloaded.ExecutionStatus);
        Assert.NotNull(reloaded.StartedAt);
        Assert.NotNull(reloaded.CompletedAt);
        Assert.NotNull(reloaded.TatRule);
        Assert.Equal("Internal", reloaded.TatRule!.Type);
    }

    [Theory]
    [InlineData("Travel & Hospitality")]
    [InlineData("EA Approval")]
    [InlineData("Delegation")]
    public async Task EaTask_NoTatModuleSnapshot_LeavesClassificationAndTatNull(string moduleName)
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, moduleName);

        var task = new EaTask
        {
            BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = "1",
            Task = "REF-2026-000001", Type = null, Subtype = null, TatRuleId = null,
            AllottedTatMinutes = null, TatUsedMinutes = null, WorkflowInstanceId = null,
            ExecutionStatus = EaTaskExecutionStatus.NotStarted,
            IsActive = true, CreatedBy = "tester", CreatedDate = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var reloaded = await db.Tasks.AsNoTracking().SingleAsync(t => t.Id == task.Id);

        Assert.Equal(moduleName, reloaded.ModuleName);
        Assert.Null(reloaded.Type);
        Assert.Null(reloaded.Subtype);
        Assert.Null(reloaded.TatRuleId);
        Assert.Null(reloaded.AllottedTatMinutes);
        Assert.Null(reloaded.TatUsedMinutes);
        Assert.Null(reloaded.WorkflowInstanceId);
        Assert.Equal(EaTaskExecutionStatus.NotStarted, reloaded.ExecutionStatus);
        Assert.Null(reloaded.StartedAt);
        Assert.Null(reloaded.CompletedAt);
    }

    [Fact]
    public async Task Approval_ClassificationSnapshot_CanBePreservedWithoutTat()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, "EA Approval");

        var task = new EaTask
        {
            BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = "APR-2026-000001",
            Task = "Laptop Purchase Approval", Type = "Verification", Subtype = "Finance",
            TatRuleId = null, AllottedTatMinutes = null, TatUsedMinutes = null,
            ExecutionStatus = EaTaskExecutionStatus.InProgress, StartedAt = DateTime.UtcNow,
            IsActive = true, CreatedBy = "tester", CreatedDate = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var reloaded = await db.Tasks.AsNoTracking().SingleAsync(t => t.Id == task.Id);
        Assert.Equal("Verification", reloaded.Type);
        Assert.Equal("Finance", reloaded.Subtype);
        Assert.Null(reloaded.TatRuleId);
        Assert.Null(reloaded.AllottedTatMinutes);
        // Approval has no distinct Start action — creation and start coincide.
        Assert.Equal(EaTaskExecutionStatus.InProgress, reloaded.ExecutionStatus);
    }

    [Theory]
    [InlineData(120, 90, 30)]
    [InlineData(60, 60, 0)]
    [InlineData(60, 90, -30)]
    [InlineData(null, 90, null)]
    [InlineData(120, null, null)]
    [InlineData(null, null, null)]
    public void CalculateTatDifferenceMinutes_ProducesExpectedSignedResult_OrNullWhenEitherInputMissing(
        int? allotted, int? used, int? expected)
    {
        Assert.Equal(expected, EaTaskService.CalculateTatDifferenceMinutes(allotted, used));
    }

    [Theory]
    [InlineData("NotStarted", true)]
    [InlineData("InProgress", true)]
    [InlineData("Completed", true)]
    [InlineData("Cancelled", true)]
    [InlineData("Paused", false)]
    [InlineData("Running", false)]
    [InlineData("Captured", false)]
    public void EaTaskExecutionStatus_IsValid_OnlyAcceptsTheFourCanonicalValues(string value, bool expected)
    {
        Assert.Equal(expected, EaTaskExecutionStatus.IsValid(value));
    }

    [Theory]
    [InlineData(false, false, "NotStarted")]
    [InlineData(true, false, "InProgress")]
    [InlineData(true, true, "Completed")]
    [InlineData(false, true, "Completed")] // completion wins even if hasStarted was somehow unset
    public void MeetingExecutionStateMapper_Map_ProducesCanonicalValue(bool hasStarted, bool isCompleted, string expected)
    {
        Assert.Equal(expected, MeetingExecutionStateMapper.Map(hasStarted, isCompleted));
    }

    [Fact]
    public async Task GetAsync_RealService_MapsEveryNewField_IntoResponseDto_IncludingDerivedDifference()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, "Meeting");
        var rule = new TatRule { BusinessModuleId = module.Id, ModuleName = "Meeting", Type = "Internal", Subtype = "Review",
            TatMinutes = 120, IsActive = true, CreatedBy = "tester", CreatedDate = DateTime.UtcNow };
        db.TatRules.Add(rule);
        await db.SaveChangesAsync();
        var task = new EaTask
        {
            BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = "1", Task = "Client Review",
            Type = "Internal", Subtype = "Review", TatRuleId = rule.Id, AllottedTatMinutes = 120, TatUsedMinutes = 90,
            ExecutionStatus = EaTaskExecutionStatus.Completed,
            StartedAt = DateTime.UtcNow.AddHours(-2), CompletedAt = DateTime.UtcNow.AddMinutes(-30),
            IsActive = true, CreatedBy = "tester", CreatedDate = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var dto = await MakeService(db).GetAsync(task.Id, default);

        Assert.Equal("Meeting", dto.ModuleName);
        Assert.Equal("Internal", dto.Type);
        Assert.Equal("Review", dto.Subtype);
        Assert.Equal(rule.Id, dto.TatRuleId);
        Assert.Equal(120, dto.AllottedTatMinutes);
        Assert.Equal(90, dto.TatUsedMinutes);
        Assert.Equal(30, dto.TatDifferenceMinutes);
        Assert.Equal(EaTaskExecutionStatus.Completed, dto.ExecutionStatus);
        Assert.NotNull(dto.StartedAt);
        Assert.NotNull(dto.CompletedAt);
        // Completed: CurrentTatUsedMinutes equals the frozen value, not a fresh live calc.
        Assert.Equal(90, dto.CurrentTatUsedMinutes);
        Assert.Equal(30, dto.CurrentTatDifferenceMinutes);
    }

    [Fact]
    public async Task GetAsync_NoTatTask_ReturnsNullTatDifference_NotZero()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, "Delegation");
        var task = new EaTask
        {
            BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = "1", Task = "DLG-2026-000001",
            AllottedTatMinutes = null, TatUsedMinutes = null, ExecutionStatus = EaTaskExecutionStatus.NotStarted,
            IsActive = true, CreatedBy = "tester", CreatedDate = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var dto = await MakeService(db).GetAsync(task.Id, default);

        Assert.Null(dto.TatDifferenceMinutes);
        Assert.Null(dto.CurrentTatUsedMinutes);
        Assert.Null(dto.CurrentTatDifferenceMinutes);
        // No WorkflowInstance at all for this module — pause info is not applicable, not false/0.
        Assert.Null(dto.IsPaused);
        Assert.Null(dto.PauseCount);
        Assert.Null(dto.TotalPausedMinutes);
    }

    [Fact]
    public async Task GetAsync_NotStartedTatTask_CurrentTatUsedMinutesIsNull_NotZero()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, "Meeting");
        var task = new EaTask
        {
            BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = "1", Task = "Not started yet",
            AllottedTatMinutes = 60, TatUsedMinutes = null, ExecutionStatus = EaTaskExecutionStatus.NotStarted,
            IsActive = true, CreatedBy = "tester", CreatedDate = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var dto = await MakeService(db).GetAsync(task.Id, default);

        Assert.Null(dto.StartedAt);
        Assert.Null(dto.CurrentTatUsedMinutes);
        Assert.Null(dto.CurrentTatDifferenceMinutes);
    }

    [Fact]
    public async Task GetAsync_InProgressTatTask_DerivesLiveCurrentTatUsedMinutes_ExcludingOpenPause()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, "Meeting");
        var status = new Status { Name = "In Progress", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.Statuses.Add(status);
        await db.SaveChangesAsync();
        var tatStart = DateTime.UtcNow.AddMinutes(-90);
        var workflow = new WorkflowInstance { StatusId = status.Id, StartedAt = tatStart, TatStartedAt = tatStart,
            IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.WorkflowInstances.Add(workflow);
        await db.SaveChangesAsync();

        var task = new EaTask
        {
            BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = "1", Task = "Running task",
            AllottedTatMinutes = 180, TatUsedMinutes = null, ExecutionStatus = EaTaskExecutionStatus.InProgress,
            StartedAt = tatStart, WorkflowInstanceId = workflow.Id,
            IsActive = true, CreatedBy = "tester", CreatedDate = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        // Closed simple pause: 60-30 minutes ago = 30 minutes paused, excluded from TAT usage.
        db.WorkPauses.Add(new WorkPause
        {
            WorkflowInstanceId = workflow.Id,
            StartAt = DateTime.UtcNow.AddMinutes(-60),
            EndAt = DateTime.UtcNow.AddMinutes(-30),
            CreatedBy = "seed", CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var dto = await MakeService(db).GetAsync(task.Id, default);

        Assert.False(dto.IsPaused);
        Assert.Equal(1, dto.PauseCount);
        Assert.InRange(dto.TotalPausedMinutes!.Value, 28, 32);
        // 90 elapsed - 30 paused ≈ 60 used (±3 for test timing).
        Assert.InRange(dto.CurrentTatUsedMinutes!.Value, 57, 63);
        Assert.InRange(dto.CurrentTatDifferenceMinutes!.Value, 117, 123);
    }

    [Fact]
    public async Task GetAsync_OpenPause_IsPausedTrue()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, "Meeting");
        var tatStart = DateTime.UtcNow.AddMinutes(-30);
        var workflow = new WorkflowInstance { StatusId = 0, StartedAt = tatStart, TatStartedAt = tatStart,
            IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.WorkflowInstances.Add(workflow);
        var task = new EaTask
        {
            BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = "1", Task = "Paused task",
            AllottedTatMinutes = 120, ExecutionStatus = EaTaskExecutionStatus.InProgress,
            StartedAt = tatStart, WorkflowInstanceId = workflow.Id,
            IsActive = true, CreatedBy = "tester", CreatedDate = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        db.WorkPauses.Add(new WorkPause
        {
            WorkflowInstanceId = workflow.Id, StartAt = DateTime.UtcNow.AddMinutes(-10), EndAt = null,
            CreatedBy = "seed", CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var dto = await MakeService(db).GetAsync(task.Id, default);

        Assert.True(dto.IsPaused);
        Assert.Equal(1, dto.PauseCount);
        // ExecutionStatus itself is unaffected by pause state — still InProgress, never a
        // separate "Paused" execution status.
        Assert.Equal(EaTaskExecutionStatus.InProgress, dto.ExecutionStatus);
    }

    [Fact]
    public async Task GetHistoryAsync_MeetingTask_ProducesChronologicalCreatedStartedPausedResumedCompleted()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, "Meeting");
        var capturedStatus = new Status { Name = "Captured", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        var inProgressStatus = new Status { Name = "In Progress", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        var completedStatus = new Status { Name = "Completed", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.Statuses.AddRange(capturedStatus, inProgressStatus, completedStatus);
        await db.SaveChangesAsync();

        var created = DateTime.UtcNow.AddHours(-3);
        var started = created.AddMinutes(5);
        var pauseStart = started.AddMinutes(20);
        var pauseEnd = pauseStart.AddMinutes(15);
        var completed = pauseEnd.AddMinutes(40);

        var workflow = new WorkflowInstance
        {
            StatusId = completedStatus.Id, StartedAt = started, TatStartedAt = started, CompletedAt = completed,
            IsActive = false, CreatedBy = "seed", CreatedDate = created
        };
        db.WorkflowInstances.Add(workflow);
        await db.SaveChangesAsync();

        db.WorkflowHistory.AddRange(
            new WorkflowHistory { WorkflowInstanceId = workflow.Id, FromStatusId = capturedStatus.Id, ToStatusId = inProgressStatus.Id,
                ChangedAt = started, CreatedBy = "alice", CreatedDate = started },
            new WorkflowHistory { WorkflowInstanceId = workflow.Id, FromStatusId = inProgressStatus.Id, ToStatusId = completedStatus.Id,
                ChangedAt = completed, CreatedBy = "bob", CreatedDate = completed });
        db.WorkPauses.Add(new WorkPause
        {
            WorkflowInstanceId = workflow.Id, StartAt = pauseStart, EndAt = pauseEnd, Reason = "Lunch",
            CreatedBy = "alice", ResumedById = "alice", ResumedByName = "Alice", ResumedReason = "Back",
            CreatedDate = pauseStart
        });

        var task = new EaTask
        {
            BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = "1", Task = "Client Review",
            AllottedTatMinutes = 120, TatUsedMinutes = 55, ExecutionStatus = EaTaskExecutionStatus.Completed,
            StartedAt = started, CompletedAt = completed, WorkflowInstanceId = workflow.Id,
            IsActive = false, CreatedBy = "creator", CreatedDate = created
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var events = await MakeService(db).GetHistoryAsync(task.Id, default);

        Assert.Equal(new[] { "Created", "Started", "Paused", "Resumed", "Completed" }, events.Select(e => e.EventType));
        Assert.True(events.SequenceEqual(events.OrderBy(e => e.OccurredAt)));
        Assert.Equal("creator", events[0].PerformedBy);
        Assert.Equal("alice", events[1].PerformedBy);
        Assert.Equal(EaTaskExecutionStatus.NotStarted, events[0].NewExecutionStatus);
        Assert.Equal(EaTaskExecutionStatus.InProgress, events[1].NewExecutionStatus);
        Assert.Equal(EaTaskExecutionStatus.Completed, events[4].NewExecutionStatus);
        Assert.Equal("bob", events[4].PerformedBy);
        Assert.Equal(55, events[4].CurrentTatUsedMinutes);
        Assert.Equal(pauseStart, events[2].PauseStartedAt);
        Assert.Equal(pauseEnd, events[3].PauseEndedAt);
        Assert.Equal(15, events[3].PauseDurationMinutes);
    }

    [Fact]
    public async Task GetHistoryAsync_TravelTask_ReadsFromExistingAuditLog_NoNewTable()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, "Travel & Hospitality");
        var created = DateTime.UtcNow.AddDays(-2);
        var task = new EaTask
        {
            BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = "7", Task = "TRV-0007",
            ExecutionStatus = EaTaskExecutionStatus.Completed, StartedAt = created.AddHours(1), CompletedAt = created.AddHours(5),
            IsActive = false, CreatedBy = "creator", CreatedDate = created
        };
        db.Tasks.Add(task);
        db.AuditLogs.AddRange(
            new AuditLog { ActionType = "TRAVEL_START", Module = "Travel", EntityName = "TravelRequest", EntityId = "7",
                ActorName = "traveller", OccurredAt = created.AddHours(1), CreatedBy = "traveller", CreatedDate = created.AddHours(1) },
            new AuditLog { ActionType = "TRAVEL_COMPLETE", Module = "Travel", EntityName = "TravelRequest", EntityId = "7",
                ActorName = "traveller", OccurredAt = created.AddHours(5), CreatedBy = "traveller", CreatedDate = created.AddHours(5) });
        await db.SaveChangesAsync();

        var events = await MakeService(db).GetHistoryAsync(task.Id, default);

        Assert.Equal(new[] { "Created", "Started", "Completed" }, events.Select(e => e.EventType));
        Assert.All(events, e => Assert.Equal(e == events[0] ? "EaTask" : "AuditLog", e.Source));
        Assert.Equal("traveller", events[1].PerformedBy);
    }

    [Fact]
    public async Task GetHistoryAsync_DelegationTask_NotYetStarted_OnlyCreated()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, "Delegation");
        var task = new EaTask
        {
            BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = "3", Task = "DLG-2026-000003",
            ExecutionStatus = EaTaskExecutionStatus.NotStarted, IsActive = true, CreatedBy = "creator", CreatedDate = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var events = await MakeService(db).GetHistoryAsync(task.Id, default);

        Assert.Single(events);
        Assert.Equal("Created", events[0].EventType);
    }

    [Fact]
    public async Task GetHistoryAsync_DelegationTask_AfterStartAndComplete_ReadsFromExistingAuditLog_NoNewTable()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, "Delegation");
        var created = DateTime.UtcNow.AddDays(-1);
        var task = new EaTask
        {
            BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = "9", Task = "DLG-2026-000009",
            ExecutionStatus = EaTaskExecutionStatus.Completed, StartedAt = created.AddHours(1), CompletedAt = created.AddHours(3),
            IsActive = false, CreatedBy = "manager-1", CreatedDate = created
        };
        db.Tasks.Add(task);
        db.AuditLogs.AddRange(
            new AuditLog { ActionType = "DELEGATION_START", Module = "Delegation", EntityName = "Delegation", EntityId = "9",
                ActorName = "doer-1", OccurredAt = created.AddHours(1), CreatedBy = "doer-1", CreatedDate = created.AddHours(1) },
            new AuditLog { ActionType = "DELEGATION_COMPLETE", Module = "Delegation", EntityName = "Delegation", EntityId = "9",
                ActorName = "doer-1", OccurredAt = created.AddHours(3), CreatedBy = "doer-1", CreatedDate = created.AddHours(3) });
        await db.SaveChangesAsync();

        var events = await MakeService(db).GetHistoryAsync(task.Id, default);

        Assert.Equal(new[] { "Created", "Started", "Completed" }, events.Select(e => e.EventType));
        Assert.True(events.SequenceEqual(events.OrderBy(e => e.OccurredAt))); // chronological
        Assert.Equal("manager-1", events[0].PerformedBy);
        Assert.Equal("doer-1", events[1].PerformedBy);
        Assert.Equal("doer-1", events[2].PerformedBy);
        Assert.Equal(EaTaskExecutionStatus.NotStarted, events[0].NewExecutionStatus);
        Assert.Equal(EaTaskExecutionStatus.InProgress, events[1].NewExecutionStatus);
        Assert.Equal(EaTaskExecutionStatus.Completed, events[2].NewExecutionStatus);
        // No fabricated Paused/Resumed events — Delegation has no pause concept.
        Assert.DoesNotContain(events, e => e.EventType is "Paused" or "Resumed");
    }
}
