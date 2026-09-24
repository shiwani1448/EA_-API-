using System;
using System.Linq;
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
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using static Jarvis5.Tests.EaFms.Followups.FollowupBusinessApiTests;

namespace Jarvis5.Tests.EaFms.Followups;

/// <summary>
/// Follow-up's own Actual-phase lifecycle (start / pause / resume / complete), TAT snapshot,
/// reminder log and central timeline — mirrors DelegationPauseResumeTests / DelegationTypeOnlyTatTests.
/// </summary>
public class FollowupLifecycleTests
{
    private sealed class Fx
    {
        public required EaFmsDbContext Db { get; init; }
        public required FollowupService Svc { get; init; }
        public required ICurrentUserService User { get; init; }
        public required long ModuleId { get; init; }
    }

    private static async Task<Fx> NewAsync(int? ruleMinutes = 120, string ruleType = "Action")
    {
        var db = MakeDb();
        var moduleId = FollowupTestSupport.EnsureFollowupModule(db);
        foreach (var name in new[] { "In Progress", "Completed" })
            db.Statuses.Add(new Status { Name = name, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        if (ruleMinutes.HasValue)
            db.TatRules.Add(new TatRule { BusinessModuleId = moduleId, ModuleName = FollowupService.FollowupBusinessModuleName,
                Type = ruleType, TaskType = DelegationTaskType.Actual, TatMinutes = ruleMinutes.Value, IsActive = true,
                CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var user = FollowupTestSupport.User("S5I-1013", "Siddhi Jadhav");
        var svc = new FollowupService(new FollowupRepository(db), db, Mapper, user, new AuditService(db, user),
            new FollowupSourceResolver(db), FollowupTestSupport.EaTasks(db), new TatRuleRepository(db));
        return new Fx { Db = db, Svc = svc, User = user, ModuleId = moduleId };
    }

    private static Task<FollowupResponseDto> CreateAsync(Fx f, string type = "Action", string subject = "Chase vendor") =>
        f.Svc.CreateAsync(new CreateFollowupRequestDto { Subject = subject, Type = type, DueAt = DateTime.UtcNow.AddDays(2), DoerId = "E-1", DoerName = "Doer" });

    private static Task<EaTask> TaskOf(Fx f, long followupId) =>
        f.Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == f.Db.Followups.Single(x => x.Id == followupId).EaTaskId);

    // ---------------- create / TAT snapshot ----------------
    [Fact]
    public async Task Create_SnapshotsActualTat_NotStarted_WithOwnEaTask()
    {
        var f = await NewAsync(ruleMinutes: 90);

        var r = await CreateAsync(f);

        Assert.NotNull(r.EaTaskId);
        Assert.Equal(EaTaskExecutionStatus.NotStarted, r.ExecutionStatus);
        Assert.Equal(90, r.AllottedTatMinutes);
        Assert.False(r.IsPaused);
        Assert.Null(r.StartedAt);
        Assert.Empty(r.PhaseTat);
        var task = await TaskOf(f, r.Id);
        Assert.Equal((FollowupService.FollowupBusinessModuleName, r.Id.ToString(), 90), (task.ModuleName, task.BusinessRecordId, task.AllottedTatMinutes));
        Assert.NotNull(task.TatRuleId);
    }

    [Fact]
    public async Task Create_WithoutRule_HasNullTat_AndNoTatSummary()
    {
        var f = await NewAsync(ruleMinutes: null);

        var r = await CreateAsync(f);

        Assert.Null(r.AllottedTatMinutes);
        Assert.Null((await TaskOf(f, r.Id)).TatRuleId);
        Assert.NotNull(r.TatSummary);
        Assert.Null(r.TatSummary!.Tat);

        var started = await f.Svc.StartAsync(r.Id);
        Assert.Null(Assert.Single(started.PhaseTat).AllottedTatMinutes);
        Assert.Null(started.PhaseTat[0].TatUsedMinutes);
    }

    [Fact]
    public async Task Update_TypeChange_ReResolvesTatBeforeStart_ButFreezesAfterStart()
    {
        var f = await NewAsync(ruleMinutes: 60, ruleType: "Action");
        f.Db.TatRules.Add(new TatRule { BusinessModuleId = f.ModuleId, ModuleName = "Follow-up", Type = "Call", TaskType = DelegationTaskType.Actual,
            TatMinutes = 15, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();
        var r = await CreateAsync(f);

        var updated = await f.Svc.UpdateAsync(r.Id, new UpdateFollowupRequestDto { Subject = "Chase", Type = "Call", DueAt = DateTime.UtcNow.AddDays(1) });
        Assert.Equal(15, updated.AllottedTatMinutes);

        await f.Svc.StartAsync(r.Id);
        var after = await f.Svc.UpdateAsync(r.Id, new UpdateFollowupRequestDto { Subject = "Chase", Type = "Action", DueAt = DateTime.UtcNow.AddDays(1) });
        Assert.Equal(15, after.AllottedTatMinutes);
        Assert.Equal(15, after.PhaseTat.Single().AllottedTatMinutes);
    }

    // Review/Rework rule rejection and the backfill run against Postgres: see FollowupTatPostgresTests.

    // ---------------- happy path ----------------
    [Fact]
    public async Task Start_Pause_Resume_Complete_HappyPath_WithTokenActors_AndAudits()
    {
        var f = await NewAsync();
        var r = await CreateAsync(f);

        var started = await f.Svc.StartAsync(r.Id);
        Assert.Equal(EaTaskExecutionStatus.InProgress, started.ExecutionStatus);
        Assert.NotNull(started.StartedAt);
        Assert.Equal(("S5I-1013", "Siddhi Jadhav"), (started.StartedById, started.StartedByName));
        Assert.Equal(DelegationTaskType.Actual, started.CurrentPhase);
        var phase = Assert.Single(started.PhaseTat);
        Assert.Equal((DelegationTaskType.Actual, 0, 120), (phase.TaskType, phase.ReviewCycleNumber, phase.AllottedTatMinutes));

        var paused = await f.Svc.PauseAsync(r.Id, new FollowupPauseRequestDto { PauseReason = "  Waiting on vendor  " });
        Assert.True(paused.IsPaused);
        var pause = Assert.Single(await f.Db.WorkPauses.AsNoTracking().ToListAsync());
        Assert.Equal("Waiting on vendor", pause.Reason);
        Assert.Null(pause.FollowupId);                       // never a dependency-wait marker
        var task = await TaskOf(f, r.Id);
        Assert.Equal(pause.WorkflowInstanceId, task.WorkflowInstanceId);
        Assert.Null(f.Db.Followups.Single(x => x.Id == r.Id).WorkflowInstanceId); // anchor lives on the EaTask only

        var resumed = await f.Svc.ResumeAsync(r.Id);
        Assert.False(resumed.IsPaused);
        pause = await f.Db.WorkPauses.AsNoTracking().SingleAsync();
        Assert.NotNull(pause.EndAt);
        Assert.Equal(("S5I-1013", "Siddhi Jadhav"), (pause.ResumedById, pause.ResumedByName));

        var done = await f.Svc.CompleteAsync(r.Id, new CompleteFollowupRequestDto { CompletionNote = "Vendor replied" });
        Assert.Equal(EaTaskExecutionStatus.Completed, done.ExecutionStatus);
        Assert.NotNull(done.CompletedAt);
        var closed = Assert.Single(done.PhaseTat);
        Assert.NotNull(closed.EndedAt);
        Assert.Equal(("S5I-1013", "Siddhi Jadhav"), (closed.EndedById, closed.EndedByName));
        Assert.Equal(EaTaskExecutionStatus.Completed, (await TaskOf(f, r.Id)).ExecutionStatus);

        var actions = await f.Db.AuditLogs.AsNoTracking().Where(a => a.EntityId == r.Id.ToString()).Select(a => a.ActionType).ToListAsync();
        foreach (var a in new[] { "FOLLOWUP_START", "FOLLOWUP_PAUSE", "FOLLOWUP_RESUME", "FOLLOWUP_COMPLETE" })
            Assert.Contains(a, actions);
    }

    [Fact]
    public async Task Pause_WithoutBody_UsesDefaultReason_AndOver2000Is400()
    {
        var f = await NewAsync();
        var r = await CreateAsync(f);
        await f.Svc.StartAsync(r.Id);

        await Assert.ThrowsAsync<BadRequestException>(() => f.Svc.PauseAsync(r.Id, new FollowupPauseRequestDto { PauseReason = new string('x', 2001) }));
        Assert.False(await f.Db.WorkPauses.AnyAsync());

        await f.Svc.PauseAsync(r.Id, null);
        Assert.Equal("Follow-up paused", (await f.Db.WorkPauses.SingleAsync()).Reason);
    }

    // ---------------- 409s ----------------
    [Fact]
    public async Task InvalidTransitions_AreAll409()
    {
        var f = await NewAsync();
        var r = await CreateAsync(f);

        // Not started: pause / resume / complete rejected.
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.PauseAsync(r.Id, null));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.ResumeAsync(r.Id));
        var notStarted = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.CompleteAsync(r.Id, new CompleteFollowupRequestDto()));
        Assert.Equal("Start the follow-up before completing it.", notStarted.Message);

        await f.Svc.StartAsync(r.Id);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.StartAsync(r.Id));   // start twice
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.ResumeAsync(r.Id));  // resume when not paused

        await f.Svc.PauseAsync(r.Id, null);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.PauseAsync(r.Id, null)); // already paused
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.CompleteAsync(r.Id, new CompleteFollowupRequestDto())); // complete while paused

        await f.Svc.ResumeAsync(r.Id);
        await f.Svc.CompleteAsync(r.Id, new CompleteFollowupRequestDto());
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.StartAsync(r.Id));     // start when completed
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.PauseAsync(r.Id, null));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.CompleteAsync(r.Id, new CompleteFollowupRequestDto()));
        Assert.Single(await f.Db.FollowupPhaseTats.ToListAsync());
    }

    // ---------------- TAT math ----------------
    [Fact]
    public async Task Complete_TatUsedExcludesPausedTime_AndPhaseValuesAreFrozen()
    {
        var f = await NewAsync(ruleMinutes: 120);
        var r = await CreateAsync(f);
        await f.Svc.StartAsync(r.Id);
        await f.Svc.PauseAsync(r.Id, null);
        await f.Svc.ResumeAsync(r.Id);

        // Backdate: started 60 min ago, paused for 30 of those minutes.
        var now = DateTime.UtcNow;
        var task = await f.Db.Tasks.SingleAsync(t => t.Id == r.EaTaskId);
        var phaseRow = await f.Db.FollowupPhaseTats.SingleAsync();
        var pause = await f.Db.WorkPauses.SingleAsync();
        task.StartedAt = phaseRow.StartedAt = now.AddMinutes(-60);
        pause.StartAt = now.AddMinutes(-50);
        pause.EndAt = now.AddMinutes(-20);
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();

        var done = await f.Svc.CompleteAsync(r.Id, new CompleteFollowupRequestDto());

        var phase = Assert.Single(done.PhaseTat);
        Assert.Equal(30, phase.TatUsedMinutes);
        Assert.Equal(30, phase.TatPausedMinutes);
        Assert.Equal(1, phase.PauseCount);
        Assert.Equal(90, phase.TatDifferenceMinutes);
        Assert.InRange((await TaskOf(f, r.Id)).TatUsedMinutes ?? -1, 29, 31);

        // Frozen: a later (bogus) pause on the anchor and the passage of time change nothing.
        f.Db.WorkPauses.Add(new WorkPause { WorkflowInstanceId = pause.WorkflowInstanceId, StartAt = now.AddMinutes(-10), EndAt = now,
            Reason = "late", CreatedBy = "x", CreatedDate = now });
        await f.Db.SaveChangesAsync();
        var reread = Assert.Single((await f.Svc.GetByIdAsync(r.Id)).PhaseTat);
        Assert.Equal((30, 30, 1), (reread.TatUsedMinutes, reread.TatPausedMinutes, reread.PauseCount));
        Assert.Equal(phase.EndedAt, reread.EndedAt);
    }

    // ---------------- list batching / views / summary ----------------
    [Fact]
    public async Task List_BatchLoadsPhaseTat_AndViewAndSummaryFilterByExecutionStatus()
    {
        var f = await NewAsync();
        var a = await CreateAsync(f, subject: "A");
        var b = await CreateAsync(f, subject: "B");
        var c = await CreateAsync(f, subject: "C");
        await f.Svc.StartAsync(a.Id);
        await f.Svc.StartAsync(b.Id);
        await f.Svc.PauseAsync(b.Id, null);

        var page = await f.Svc.GetPagedAsync(new FollowupListQueryDto());
        var byId = page.Items.ToDictionary(x => x.Id);
        Assert.Single(byId[a.Id].PhaseTat);
        Assert.True(byId[b.Id].IsPaused);
        Assert.Empty(byId[c.Id].PhaseTat);
        Assert.All(page.Items, x => Assert.Equal(120, x.AllottedTatMinutes));

        var notStarted = await f.Svc.GetPagedAsync(new FollowupListQueryDto { View = "notstarted" });
        Assert.Equal(new[] { c.Id }, notStarted.Items.Select(x => x.Id));
        var started = await f.Svc.GetPagedAsync(new FollowupListQueryDto { View = "started" });
        Assert.Equal(new[] { a.Id, b.Id }.OrderBy(x => x), started.Items.Select(x => x.Id).OrderBy(x => x));
        await Assert.ThrowsAsync<BadRequestException>(() => f.Svc.GetPagedAsync(new FollowupListQueryDto { View = "bogus" }));

        Assert.Equal(1, (await f.Svc.GetSummaryAsync(new FollowupListQueryDto())).NotStarted);
    }

    // ---------------- reminder log ----------------
    [Fact]
    public async Task ReminderLog_StoresTokenActor_IgnoresBodyIdentity_NewestFirst()
    {
        var f = await NewAsync();
        var r = await CreateAsync(f);

        var first = await f.Svc.LogReminderAsync(r.Id, new LogFollowupReminderRequestDto
            { Channel = "Email", Recipient = "vendor@acme.com", RecipientName = "Acme", Message = "Please reply", EmployeeId = "HACK", EmployeeName = "Mallory" });
        await Task.Delay(5);
        await f.Svc.LogReminderAsync(r.Id, new LogFollowupReminderRequestDto { Channel = "WhatsApp", Recipient = "+919999999999", Message = "Ping" });

        Assert.Equal(("S5I-1013", "Siddhi Jadhav"), (first.SentById, first.SentByName));
        var log = await f.Svc.GetReminderLogAsync(r.Id);
        Assert.Equal(new[] { "WhatsApp", "Email" }, log.Select(l => l.Channel));

        await Assert.ThrowsAsync<BadRequestException>(() => f.Svc.LogReminderAsync(r.Id, new LogFollowupReminderRequestDto { Channel = "Sms", Recipient = "x", Message = "m" }));
        await Assert.ThrowsAsync<BadRequestException>(() => f.Svc.LogReminderAsync(r.Id, new LogFollowupReminderRequestDto { Channel = "Email", Recipient = " ", Message = "m" }));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Svc.GetReminderLogAsync(999_999));
    }

    // ---------------- timeline ----------------
    [Fact]
    public async Task Timeline_ContainsCreatedStartedPausedResumedReminderRecordedCompleted_InOrder()
    {
        var f = await NewAsync();
        var r = await CreateAsync(f);
        await f.Svc.StartAsync(r.Id);
        await Task.Delay(5);
        await f.Svc.PauseAsync(r.Id, new FollowupPauseRequestDto { PauseReason = "Waiting" });
        await Task.Delay(5);
        await f.Svc.ResumeAsync(r.Id);
        await Task.Delay(5);
        await f.Svc.LogReminderAsync(r.Id, new LogFollowupReminderRequestDto { Channel = "Email", Recipient = "v@acme.com", RecipientName = "Acme", Message = "Nudge" });
        await Task.Delay(5);
        await f.Svc.RecordFollowupAsync(r.Id, new RecordFollowupRequestDto { Note = "Called vendor" });
        await Task.Delay(5);
        await f.Svc.CompleteAsync(r.Id, new CompleteFollowupRequestDto { CompletionNote = "Closed out" });

        var eaTasks = new EaTaskService(f.Db, new EaTaskRepository(f.Db), new TatRuleRepository(f.Db), new CreateEaTaskDtoValidator(), f.User, new AuditService(f.Db, f.User));
        var events = await eaTasks.GetHistoryAsync(r.EaTaskId!.Value, default);

        Assert.Equal(new[] { "Created", "Started", "Paused", "Resumed", "ReminderSent", "FollowupRecorded", "Completed" }, events.Select(e => e.EventType));
        var reminder = events.Single(e => e.EventType == "ReminderSent");
        Assert.Equal(("FollowupReminderLog", "Email to v@acme.com (Acme)", "S5I-1013"), (reminder.Source, reminder.Notes, reminder.PerformedBy));
        Assert.Equal("Closed out", events.Single(e => e.EventType == "Completed").Notes);
        Assert.Equal("Siddhi Jadhav", events.Single(e => e.EventType == "Started").PerformedBy);
    }
}
