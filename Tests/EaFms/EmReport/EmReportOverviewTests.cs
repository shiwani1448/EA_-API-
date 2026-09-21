using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Jarvis5.Tests.EaFms.EmReport;

public class EmReportOverviewTests
{
    private static EaFmsDbContext Db() => new(new DbContextOptionsBuilder<EaFmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static readonly DateTime Base = new(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc);

    private static async Task<Dictionary<string, BusinessModule>> Modules(EaFmsDbContext db)
    {
        var all = new[] { "Meeting", "EA Approval", "Travel & Hospitality", "Delegation" }.Select(x => new BusinessModule { Name = x, IsActive = true, CreatedBy = "seed", CreatedDate = Base }).ToList();
        db.BusinessModules.AddRange(all); await db.SaveChangesAsync(); return all.ToDictionary(x => x.Name);
    }

    private static EaTask Task(BusinessModule module, string recordId, string status, DateTime? created = null) => new()
    {
        BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = recordId, Task = recordId,
        ExecutionStatus = status, CreatedBy = "seed", CreatedDate = created ?? Base, IsActive = true
    };

    [Fact]
    public async Task Overview_UsesCreatedDateFiltersAndMutuallyExclusiveExecutionCounts()
    {
        await using var db = Db(); var modules = await Modules(db);
        var workflow = new WorkflowInstance { BusinessModuleId = modules["Meeting"].Id, BusinessRecordId = "pause", CreatedBy = "seed", CreatedDate = Base };
        db.WorkflowInstances.Add(workflow); await db.SaveChangesAsync();
        var notStarted = Task(modules["Meeting"], "n", EaTaskExecutionStatus.NotStarted);
        var progress = Task(modules["Meeting"], "p", EaTaskExecutionStatus.InProgress);
        var paused = Task(modules["Meeting"], "pause", EaTaskExecutionStatus.InProgress); paused.WorkflowInstanceId = workflow.Id;
        var complete = Task(modules["Meeting"], "c", EaTaskExecutionStatus.Completed); complete.CompletedAt = Base;
        var cancelled = Task(modules["Meeting"], "x", EaTaskExecutionStatus.Cancelled);
        var outside = Task(modules["Meeting"], "old", EaTaskExecutionStatus.NotStarted, Base.AddDays(-2));
        db.Tasks.AddRange(notStarted, progress, paused, complete, cancelled, outside);
        db.WorkPauses.Add(new WorkPause { WorkflowInstanceId = workflow.Id, StartAt = Base, CreatedBy = "seed", CreatedDate = Base });
        await db.SaveChangesAsync();

        var result = await new EmReportService(db).GetOverviewAsync(new() { FromDate = new DateTime(2026, 9, 10), ToDate = new DateTime(2026, 9, 10), BusinessModuleId = modules["Meeting"].Id }, default);

        Assert.Equal(5, result.TotalTasks); Assert.Equal(1, result.NotStarted); Assert.Equal(1, result.InProgress);
        Assert.Equal(1, result.Paused); Assert.Equal(1, result.Completed); Assert.Equal(1, result.Cancelled);
        Assert.Equal(25m, result.CompletionPercentage);
    }

    [Fact]
    public async Task Overview_DateRangeUsesInclusiveIndiaBusinessDates()
    {
        await using var db = Db(); var modules = await Modules(db); var meeting = modules["Meeting"];
        // 18:30 UTC is midnight in India. Only the middle row belongs to 10 September IST.
        db.Tasks.AddRange(
            Task(meeting, "before", EaTaskExecutionStatus.NotStarted, new DateTime(2026, 9, 9, 18, 29, 59, DateTimeKind.Utc)),
            Task(meeting, "inside", EaTaskExecutionStatus.NotStarted, new DateTime(2026, 9, 9, 18, 30, 0, DateTimeKind.Utc)),
            Task(meeting, "after", EaTaskExecutionStatus.NotStarted, new DateTime(2026, 9, 10, 18, 30, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();
        var result = await new EmReportService(db).GetOverviewAsync(new() { FromDate = new DateTime(2026, 9, 10), ToDate = new DateTime(2026, 9, 10) }, default);
        Assert.Equal(1, result.TotalTasks);
    }

    [Fact]
    public async Task Overview_MeetingUsesCanonicalPausedTatAndClassifiesCompletedKpis()
    {
        await using var db = Db(); var modules = await Modules(db); var meeting = modules["Meeting"];
        var onTime = Task(meeting, "on", EaTaskExecutionStatus.Completed); onTime.AllottedTatMinutes = 60; onTime.TatUsedMinutes = 60; onTime.CompletedAt = Base;
        var delayed = Task(meeting, "late", EaTaskExecutionStatus.Completed); delayed.AllottedTatMinutes = 60; delayed.TatUsedMinutes = 61; delayed.CompletedAt = Base;
        var missing = Task(meeting, "missing", EaTaskExecutionStatus.Completed); missing.CompletedAt = Base;
        db.Tasks.AddRange(onTime, delayed, missing); await db.SaveChangesAsync();
        var result = await new EmReportService(db).GetOverviewAsync(new(), default);
        Assert.Equal(1, result.OnTimeCompleted); Assert.Equal(1, result.DelayedCompleted); Assert.Equal(1, result.NotMeasuredCompleted);
        Assert.Equal(50m, result.OnTimeCompletionPercentage); Assert.Equal(1, result.DelayedTasks);
    }

    [Fact]
    public async Task Overview_BatchesNonTatDeadlinesAndTreatsMissingOrUnmatchedAsNotMeasured()
    {
        await using var db = Db(); var modules = await Modules(db); var today = IndiaBusinessCalendar.Today;
        var approval = Task(modules["EA Approval"], "APR-1", EaTaskExecutionStatus.Completed); approval.CompletedAt = today.AddDays(1);
        var travel = Task(modules["Travel & Hospitality"], "77", EaTaskExecutionStatus.Completed); travel.CompletedAt = today;
        var delegation = Task(modules["Delegation"], "88", EaTaskExecutionStatus.InProgress);
        var malformed = Task(modules["Travel & Hospitality"], "not-an-id", EaTaskExecutionStatus.Completed); malformed.CompletedAt = today;
        db.Tasks.AddRange(approval, travel, delegation, malformed); await db.SaveChangesAsync();
        db.ApprovalRequests.Add(new ApprovalRequest { EaTaskId = approval.Id, ReferenceNo = "APR-1", RequiredApprovalDate = today, CreatedBy = "seed", CreatedAt = Base });
        db.TravelRequests.Add(new TravelRequest { Id = 77, EaTaskId = travel.Id, ReferenceNo = "T", RequiredDate = today, CreatedBy = "seed", CreatedDate = Base });
        db.Delegations.Add(new Jarvis5.Entities.EaFms.Delegation { Id = 88, EaTaskId = delegation.Id, ReferenceNo = "D", Title = "D", DoerId = "E", AssignedById = "E", DueDate = today.AddDays(-1), CreatedBy = "seed", CreatedDate = Base });
        await db.SaveChangesAsync();
        var result = await new EmReportService(db).GetOverviewAsync(new(), default);
        Assert.Equal(1, result.OnTimeCompleted); Assert.Equal(1, result.DelayedCompleted); Assert.Equal(1, result.NotMeasuredCompleted); Assert.Equal(2, result.DelayedTasks);
    }

    [Fact]
    public async Task Overview_InvalidRangeThrowsAndDeletedRowsAreExcluded()
    {
        await using var db = Db(); var modules = await Modules(db);
        var deleted = Task(modules["Meeting"], "deleted", EaTaskExecutionStatus.Completed); deleted.IsDeleted = true; db.Tasks.Add(deleted); await db.SaveChangesAsync();
        var service = new EmReportService(db);
        Assert.Equal(0, (await service.GetOverviewAsync(new(), default)).TotalTasks);
        await Assert.ThrowsAsync<BadRequestException>(() => service.GetOverviewAsync(new() { FromDate = Base.AddDays(1), ToDate = Base }, default));
    }

    [Fact]
    public async Task Attention_CountsDueTodayAndReusesTheOverviewDelayedClassifier()
    {
        await using var db = Db(); var modules = await Modules(db); var today = IndiaBusinessCalendar.Today;
        var approval = Task(modules["EA Approval"], "APR-DUE", EaTaskExecutionStatus.InProgress);
        var travel = Task(modules["Travel & Hospitality"], "71", EaTaskExecutionStatus.InProgress);
        var delegation = Task(modules["Delegation"], "81", EaTaskExecutionStatus.Completed); delegation.CompletedAt = today;
        var meeting = Task(modules["Meeting"], "meeting", EaTaskExecutionStatus.InProgress); meeting.AllottedTatMinutes = 1; meeting.StartedAt = Clock.UtcNowTz.AddMinutes(-2);
        db.Tasks.AddRange(approval, travel, delegation, meeting); await db.SaveChangesAsync();
        db.ApprovalRequests.Add(new ApprovalRequest { EaTaskId = approval.Id, ReferenceNo = "APR-DUE", RequiredApprovalDate = today, CreatedBy = "seed", CreatedAt = Base });
        db.TravelRequests.Add(new TravelRequest { Id = 71, EaTaskId = travel.Id, ReferenceNo = "T", RequiredDate = today, CreatedBy = "seed", CreatedDate = Base });
        db.Delegations.Add(new Jarvis5.Entities.EaFms.Delegation { Id = 81, EaTaskId = delegation.Id, ReferenceNo = "D", Title = "D", DoerId = "E", AssignedById = "E", DueDate = today, CreatedBy = "seed", CreatedDate = Base });
        await db.SaveChangesAsync();
        var service = new EmReportService(db);
        var attention = await service.GetAttentionAsync(new(), default);
        var overview = await service.GetOverviewAsync(new(), default);
        Assert.Equal(2, attention.DueToday); // completed Delegation and Meeting do not contribute.
        Assert.Equal(overview.DelayedTasks, attention.Delayed);
    }

    [Fact]
    public async Task Attention_FollowupsAreDistinctTasksAndRemainIndependentOfParentStatus()
    {
        await using var db = Db(); var modules = await Modules(db); var meeting = modules["Meeting"];
        var completed = Task(meeting, "1", EaTaskExecutionStatus.Completed); completed.CompletedAt = Base;
        var cancelled = Task(meeting, "2", EaTaskExecutionStatus.Cancelled);
        var allDone = Task(meeting, "3", EaTaskExecutionStatus.InProgress);
        db.Tasks.AddRange(completed, cancelled, allDone); await db.SaveChangesAsync();
        db.Followups.AddRange(
            new Followup { BusinessModuleId = meeting.Id, BusinessRecordId = "1", DueAt = Base, CreatedBy = "seed", CreatedDate = Base },
            new Followup { BusinessModuleId = meeting.Id, BusinessRecordId = "1", DueAt = Base, CreatedBy = "seed", CreatedDate = Base },
            new Followup { BusinessModuleId = meeting.Id, BusinessRecordId = "2", DueAt = Base, CreatedBy = "seed", CreatedDate = Base },
            new Followup { BusinessModuleId = meeting.Id, BusinessRecordId = "3", DueAt = Base, CompletedAt = Base, CreatedBy = "seed", CreatedDate = Base });
        await db.SaveChangesAsync();
        var attention = await new EmReportService(db).GetAttentionAsync(new(), default);
        Assert.Equal(2, attention.TasksRequiringFollowup);
    }

    [Fact]
    public async Task Attention_CountsUnresolvedEscalationRowsOnlyForCohortSources()
    {
        await using var db = Db(); var modules = await Modules(db); var meeting = modules["Meeting"];
        var inside = Task(meeting, "11", EaTaskExecutionStatus.Completed); inside.CompletedAt = Base;
        var outside = Task(meeting, "12", EaTaskExecutionStatus.InProgress, Base.AddDays(-10));
        db.Tasks.AddRange(inside, outside); await db.SaveChangesAsync();
        var includedFollowup = new Followup { BusinessModuleId = meeting.Id, BusinessRecordId = "11", DueAt = Base, CreatedBy = "seed", CreatedDate = Base };
        var excludedFollowup = new Followup { BusinessModuleId = meeting.Id, BusinessRecordId = "12", DueAt = Base, CreatedBy = "seed", CreatedDate = Base };
        db.Followups.AddRange(includedFollowup, excludedFollowup); await db.SaveChangesAsync();
        db.Escalations.AddRange(
            new Escalation { FollowupId = includedFollowup.Id, EscalationLevelId = 1, InitiatedAt = Base, CreatedBy = "seed", CreatedDate = Base },
            new Escalation { FollowupId = includedFollowup.Id, EscalationLevelId = 1, AcknowledgedAt = Base, InitiatedAt = Base, CreatedBy = "seed", CreatedDate = Base },
            new Escalation { FollowupId = includedFollowup.Id, EscalationLevelId = 1, ResolvedAt = Base, InitiatedAt = Base, CreatedBy = "seed", CreatedDate = Base },
            new Escalation { FollowupId = excludedFollowup.Id, EscalationLevelId = 1, InitiatedAt = Base, CreatedBy = "seed", CreatedDate = Base });
        await db.SaveChangesAsync();
        var attention = await new EmReportService(db).GetAttentionAsync(new() { FromDate = new DateTime(2026, 9, 10), ToDate = new DateTime(2026, 9, 10) }, default);
        Assert.Equal(2, attention.OpenEscalations);
    }
}
