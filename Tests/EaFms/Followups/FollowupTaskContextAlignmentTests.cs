using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using static Jarvis5.Tests.EaFms.Followups.FollowupBusinessApiTests;

namespace Jarvis5.Tests.EaFms.Followups;

/// <summary>
/// Followup = Reminder: one record, enriched with the central EaTask context (eaTaskId, moduleName,
/// task, stage, isPaused) matched on BusinessModuleId + BusinessRecordId, and Followup.Note exposed as
/// the canonical "remark". No duplicate storage, no side effects.
/// </summary>
public class FollowupTaskContextAlignmentTests
{

    private static FollowupService Svc(EaFmsDbContext db, IFollowupSourceResolver? resolver = null) => new(
        new FollowupRepository(db), db, Mapper,
        Mock.Of<ICurrentUserService>(u => u.UserId == 7 && u.UserName == "EA User"),
        Mock.Of<IAuditService>(), resolver ?? new FollowupSourceResolver(db), FollowupTestSupport.EaTasks(db), new TatRuleRepository(db));

    private static EaTask AddTask(EaFmsDbContext db, BusinessModule m, string recordId, string title, string status = "NotStarted", long? workflowId = null)
        => db.Tasks.Add(new EaTask
        {
            BusinessModuleId = m.Id, ModuleName = m.Name, BusinessRecordId = recordId, Task = title, ExecutionStatus = status,
            WorkflowInstanceId = workflowId, CreatedBy = "1", CreatedDate = Base
        }).Entity;

    /// <summary>Central tasks for each supported module record; Approval's is keyed by ReferenceNo.</summary>
    private static async Task<(Seed S, Dictionary<string, EaTask> Tasks)> SeedTasksAsync()
    {
        var s = await SeedAsync();
        var t = new Dictionary<string, EaTask>
        {
            ["Meeting"] = AddTask(s.Db, s.Modules["Meeting"], s.Meeting.Id.ToString(), "Prepare MOM", "InProgress"),
            ["Travel & Hospitality"] = AddTask(s.Db, s.Modules["Travel & Hospitality"], s.Travel.Id.ToString(), "TRV-2026-000001"),
            ["EA Approval"] = AddTask(s.Db, s.Modules["EA Approval"], s.Approval.ReferenceNo, "Laptop purchase", "InProgress"),
            ["Delegation"] = AddTask(s.Db, s.Modules["Delegation"], s.Delegation.Id.ToString(), "Chase vendor")
        };
        await s.Db.SaveChangesAsync();
        return (s, t);
    }

    private static string RecordId(Seed s, string module) => module switch
    {
        "Meeting" => s.Meeting.Id.ToString(), "Travel & Hospitality" => s.Travel.Id.ToString(),
        "Delegation" => s.Delegation.Id.ToString(), _ => s.Approval.ReferenceNo
    };

    private static CreateFollowupRequestDto Give(Seed s, string module, string? remark = "Need update from design team") => new()
    {
        BusinessModuleId = s.Modules[module].Id, BusinessRecordId = RecordId(s, module), Remark = remark,
        Subject = "Chase", DueAt = Base.AddDays(3), ReminderAt = Base.AddDays(1), ReminderSendEmail = true, ReminderRecipientUserId = 1, ReminderRecipientEmail = "anurag@example.com"
    };

    // 1-8, 13: every module's Give Reminder response carries the task context and reminder configuration.
    [Theory]
    [InlineData("Meeting", "Prepare MOM", "InProgress")]
    [InlineData("Travel & Hospitality", "TRV-2026-000001", "NotStarted")]
    [InlineData("EA Approval", "Laptop purchase", "InProgress")]
    [InlineData("Delegation", "Chase vendor", "NotStarted")]
    public async Task GiveReminder_ReturnsCentralTaskContext_ForEverySupportedModule(string module, string task, string stage)
    {
        var (s, tasks) = await SeedTasksAsync(); await using var _ = s.Db;

        var f = await Svc(s.Db).CreateAsync(Give(s, module));

        Assert.Equal(tasks[module].Id, f.SourceEaTaskId);
        Assert.Equal(RecordId(s, module), f.BusinessRecordId);          // source identity preserved separately
        Assert.Equal((s.Modules[module].Id, module, task, stage), (f.BusinessModuleId, f.ModuleName, f.Task, f.Stage));
        Assert.Equal("Need update from design team", f.Remark);
        Assert.Equal((true, false, 1, null), (f.ReminderSendEmail, f.ReminderSendWhatsApp, f.ReminderRecipientUserId, f.ReminderWhatsAppNumber));
        Assert.Equal(Base.AddDays(1), f.ReminderAt);
    }

    [Fact]
    public async Task ApprovalFollowup_MatchesTheTaskByReferenceNo_NotByNumericId()
    {
        var (s, tasks) = await SeedTasksAsync(); await using var _ = s.Db;

        var f = await Svc(s.Db).CreateAsync(Give(s, "EA Approval"));

        Assert.Equal("APR-2026-000009", f.BusinessRecordId);
        Assert.Equal(tasks["EA Approval"].Id, f.SourceEaTaskId);
    }

    // 5: a future EaTask-backed module needs no hardcoded module logic.
    [Fact]
    public async Task FutureModuleFollowup_GetsTaskContextFromItsEaTask()
    {
        var (s, _) = await SeedTasksAsync(); await using var _ = s.Db;
        var m = s.Modules["Vendor Management"];
        var t = AddTask(s.Db, m, "V-9", "Onboard vendor", "Completed");
        await s.Db.SaveChangesAsync();

        var f = await Svc(s.Db).CreateAsync(new CreateFollowupRequestDto { BusinessModuleId = m.Id, BusinessRecordId = "V-9", Remark = "r" });

        Assert.Equal((t.Id, "Vendor Management", "Onboard vendor", "Completed"), (f.SourceEaTaskId, f.ModuleName, f.Task, f.Stage));
    }

    // 9-10: pause is derived; Stage never becomes "Paused".
    [Fact]
    public async Task PausedMeetingTask_KeepsStageInProgress_AndExposesIsPaused()
    {
        var (s, tasks) = await SeedTasksAsync(); await using var _ = s.Db;
        var wf = new WorkflowInstance { BusinessModuleId = s.Modules["Meeting"].Id, BusinessRecordId = s.Meeting.Id.ToString(), CreatedBy = "seed", CreatedDate = Base };
        s.Db.WorkflowInstances.Add(wf);
        await s.Db.SaveChangesAsync();
        tasks["Meeting"].WorkflowInstanceId = wf.Id;
        s.Db.WorkPauses.Add(new WorkPause { WorkflowInstanceId = wf.Id, StartAt = Base, CreatedBy = "seed", CreatedDate = Base });
        await s.Db.SaveChangesAsync();
        var svc = Svc(s.Db);

        var paused = await svc.CreateAsync(Give(s, "Meeting"));
        var travel = await svc.CreateAsync(Give(s, "Travel & Hospitality"));
        s.Db.WorkPauses.Single().EndAt = Base.AddHours(1);
        await s.Db.SaveChangesAsync();
        var resumed = await svc.GetByIdAsync(paused.Id);

        Assert.Equal(("InProgress", true), (paused.Stage, paused.SourceIsPaused));
        Assert.Null(travel.SourceIsPaused);                            // no pause infrastructure -> null, as in the task API
        Assert.Equal(("InProgress", false), (resumed.Stage, resumed.SourceIsPaused));
        Assert.DoesNotContain("Paused", new[] { paused.Stage, resumed.Stage });
    }

    // 11-12: remark is Followup.Note - one stored value.
    [Fact]
    public async Task Remark_IsStoredInNote_AndOldNoteCallersStillWork()
    {
        await using var db = MakeDb();
        var svc = Svc(db);

        var viaRemark = await svc.CreateAsync(new CreateFollowupRequestDto { Remark = "  from remark  " });
        var viaNote = await svc.CreateAsync(new CreateFollowupRequestDto { Note = "from note" });
        var both = await svc.CreateAsync(new CreateFollowupRequestDto { Remark = "same", Note = "same" });

        Assert.Equal(("from remark", "from remark"), (viaRemark.Remark, viaRemark.Note));
        Assert.Equal(("from note", "from note"), (viaNote.Remark, viaNote.Note));
        Assert.Equal("same", both.Remark);
        Assert.Equal("from remark", (await db.Followups.AsNoTracking().SingleAsync(f => f.Id == viaRemark.Id)).Note);
        Assert.False(typeof(Followup).GetProperties().Any(p => p.Name == "Remark"));   // no duplicate column
    }

    [Fact]
    public async Task ConflictingRemarkAndNote_AreRejected_OnCreateAndUpdate()
    {
        await using var db = MakeDb();
        var svc = Svc(db);
        var f = await svc.CreateAsync(new CreateFollowupRequestDto { Remark = "a" });

        await Assert.ThrowsAsync<BadRequestException>(() => svc.CreateAsync(new CreateFollowupRequestDto { Remark = "a", Note = "b" }));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.UpdateAsync(f.Id, new UpdateFollowupRequestDto { Remark = "a", Note = "b" }));
    }

    [Fact]
    public async Task Update_ChangesRemark_KeepsTaskContext_AndReminderConfiguration()
    {
        var (s, tasks) = await SeedTasksAsync(); await using var _ = s.Db;
        var svc = Svc(s.Db);
        var f = await svc.CreateAsync(Give(s, "Delegation"));

        var u = await svc.UpdateAsync(f.Id, new UpdateFollowupRequestDto
        { Remark = "new remark", DueAt = Base.AddDays(3), ReminderAt = Base.AddDays(1), ReminderSendEmail = true, ReminderRecipientUserId = 1, ReminderRecipientEmail = "anurag@example.com" });

        Assert.Equal(("new remark", tasks["Delegation"].Id, "Chase vendor", true), (u.Remark, u.SourceEaTaskId, u.Task, u.ReminderSendEmail));
    }

    // 14-17: single, list and source-filtered views are the same record; reading creates nothing.
    [Fact]
    public async Task SingleListAndSourceFilter_ReturnTheSameEnrichedRecord_WithoutCreatingAnything()
    {
        var (s, tasks) = await SeedTasksAsync(); await using var _ = s.Db;
        var svc = Svc(s.Db);
        var created = await svc.CreateAsync(Give(s, "Travel & Hospitality"));
        var counts = (Followups: await s.Db.Followups.CountAsync(), Tasks: await s.Db.Tasks.CountAsync());

        var single = await svc.GetByIdAsync(created.Id);
        var general = (await svc.GetPagedAsync(new FollowupListQueryDto())).Items.Single();
        var bySource = (await svc.GetPagedAsync(new FollowupListQueryDto
        { BusinessModuleId = s.Modules["Travel & Hospitality"].Id, BusinessRecordId = s.Travel.Id.ToString() })).Items.Single();

        foreach (var r in new[] { single, general, bySource })
        {
            Assert.Equal(created.Id, r.Id);
            Assert.Equal((tasks["Travel & Hospitality"].Id, "Travel & Hospitality", "TRV-2026-000001", "NotStarted"), (r.SourceEaTaskId, r.ModuleName, r.Task, r.Stage));
            Assert.Equal((true, "Need update from design team"), (r.ReminderSendEmail, r.Remark));
        }
        Assert.Equal(counts, (await s.Db.Followups.CountAsync(), await s.Db.Tasks.CountAsync()));
    }

    // 18: an EaTask alone never creates a Followup; the workspace stays read-only.
    [Fact]
    public async Task EveryTask_DoesNotAutomaticallyGetAFollowup()
    {
        var (s, _) = await SeedTasksAsync(); await using var _ = s.Db;
        var tasks = new EaTaskService(s.Db, new EaTaskRepository(s.Db), new TatRuleRepository(s.Db), new Jarvis5.Validators.CreateEaTaskDtoValidator(),
            Mock.Of<ICurrentUserService>(u => u.UserId == 1), Mock.Of<IAuditService>());

        var ws = await tasks.QueryWorkspaceAsync(new EaTaskWorkspaceQueryDto(), default);
        var list = await Svc(s.Db).GetPagedAsync(new FollowupListQueryDto());

        Assert.Equal(4, ws.TotalCount);
        Assert.Equal(0, list.TotalCount);
        Assert.Equal(0, await s.Db.Followups.CountAsync());
    }

    // 19: several follow-ups per source remain allowed (no unique constraint).
    [Fact]
    public async Task MultipleFollowupsForOneSource_AreSupported_AndShareTheSameTask()
    {
        var (s, tasks) = await SeedTasksAsync(); await using var _ = s.Db;
        var svc = Svc(s.Db);

        var a = await svc.CreateAsync(Give(s, "Meeting", "first"));
        var b = await svc.CreateAsync(Give(s, "Meeting", "second"));
        var page = await svc.GetPagedAsync(new FollowupListQueryDto { BusinessModuleId = s.Modules["Meeting"].Id, BusinessRecordId = s.Meeting.Id.ToString() });

        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(new[] { "first", "second" }, page.Items.Select(i => i.Remark).OrderBy(x => x));
        Assert.All(page.Items, i => Assert.Equal(tasks["Meeting"].Id, i.SourceEaTaskId));
        // The 4 source tasks are untouched; each follow-up owns exactly one "Follow-up" task.
        Assert.Equal(4, await s.Db.Tasks.CountAsync(t => t.ModuleName != "Follow-up"));
        Assert.Equal(2, await s.Db.Tasks.CountAsync(t => t.ModuleName == "Follow-up"));
        Assert.NotEqual(a.EaTaskId, b.EaTaskId);
    }

    // 20: escalation still hangs off the Followup.
    [Fact]
    public async Task Escalation_StillLinksToTheFollowup_AndInheritsItsSource()
    {
        var (s, _) = await SeedTasksAsync(); await using var _ = s.Db;
        var f = await Svc(s.Db).CreateAsync(Give(s, "Meeting"));
        s.Db.EscalationLevels.Add(new EscalationLevel { Id = 1, Code = "L1", Name = "L1", Level = 1, CreatedBy = "seed", CreatedDate = Base });
        await s.Db.SaveChangesAsync();
        var repo = new EscalationRepository(s.Db);
        var esc = new EscalationService(repo, s.Db, Mapper, Mock.Of<ICurrentUserService>(u => u.UserId == 7 && u.UserName == "EA User"), Mock.Of<IAuditService>());

        var created = await esc.CreateAsync(new CreateEscalationRequestDto { FollowupId = f.Id, EscalationLevelId = 1 });

        Assert.Equal(f.Id, created.FollowupId);
        Assert.Equal((f.BusinessModuleId, f.BusinessRecordId), (created.BusinessModuleId, created.BusinessRecordId));
        Assert.Equal(1, (await esc.GetByFollowupIdAsync(f.Id)).Count);
    }

    // 21-25: no side effects.
    [Fact]
    public async Task GiveReminder_CreatesOnlyItsOwnFollowupTask_NoNotification_OrEscalation()
    {
        var (s, _) = await SeedTasksAsync(); await using var _ = s.Db;
        var before = await s.Db.Tasks.CountAsync();

        var f = await Svc(s.Db).CreateAsync(Give(s, "EA Approval"));

        Assert.Equal(before + 1, await s.Db.Tasks.CountAsync());
        var own = await s.Db.Tasks.SingleAsync(t => t.ModuleName == "Follow-up");
        Assert.Equal((f.EaTaskId, f.Id.ToString(), "NotStarted"), ((long?)own.Id, own.BusinessRecordId, own.ExecutionStatus));
        Assert.Equal(0, await s.Db.Notifications.CountAsync());
        Assert.Equal(0, await s.Db.Escalations.CountAsync());
        Assert.Equal(1, await s.Db.Followups.CountAsync());
    }

    // 26: enrichment is batched. Task-backed rows never fall back to the per-row source resolver.
    [Fact]
    public async Task ListEnrichment_UsesNoPerRowLookups_ForTaskBackedRows()
    {
        var (s, _) = await SeedTasksAsync(); await using var _ = s.Db;
        var seedSvc = Svc(s.Db);
        foreach (var m in new[] { "Meeting", "Travel & Hospitality", "EA Approval", "Delegation" })
            for (var i = 0; i < 3; i++) await seedSvc.CreateAsync(Give(s, m, $"{m} {i}"));
        var resolver = new Mock<IFollowupSourceResolver>();

        var page = await Svc(s.Db, resolver.Object).GetPagedAsync(new FollowupListQueryDto { PageSize = 50 });

        Assert.Equal(12, page.Items.Count);
        Assert.All(page.Items, i => Assert.NotNull(i.SourceEaTaskId));
        resolver.Verify(r => r.ResolveAsync(It.IsAny<long?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FollowupWithoutACentralTask_StillReturns_WithSourceTitleFallback_AndNullTaskContext()
    {
        var s = await SeedAsync(); await using var _ = s.Db;   // records exist, but no EaTask rows

        var f = await Svc(s.Db).CreateAsync(Give(s, "Meeting"));

        Assert.Null(f.SourceEaTaskId);
        Assert.Null(f.Stage);
        Assert.Equal("Meeting", f.ModuleName);
        Assert.Equal("Board meeting", f.BusinessRecordTitle);
    }

    // 27-29: existing behaviours.
    [Fact]
    public async Task IntakeFollowup_RecordFollowup_AndComplete_StillWork()
    {
        await using var db = MakeDb();
        var intake = new IntakeRequest { Title = "i", CreatedBy = "seed", CreatedDate = Base };
        db.IntakeRequests.Add(intake);
        await db.SaveChangesAsync();
        var svc = Svc(db);

        var f = await svc.CreateAsync(new CreateFollowupRequestDto { IntakeRequestId = intake.Id, DueAt = Base, Note = "call client" });
        Assert.Null(f.SourceEaTaskId);
        Assert.Null(f.ModuleName);
        Assert.Equal("call client", f.Remark);

        await svc.RecordFollowupAsync(f.Id, new RecordFollowupRequestDto { Note = "left voicemail" });
        var recorded = await svc.GetByIdAsync(f.Id);
        Assert.Equal("left voicemail", recorded.Remark);        // record-followup updates the same remark
        Assert.NotNull(recorded.LastFollowupAt);

        await svc.StartAsync(f.Id);
        var done = await svc.CompleteAsync(f.Id, new CompleteFollowupRequestDto { CompletionNote = "ok" });
        Assert.True(done.IsCompleted);
        Assert.Equal(1, (await svc.GetByIntakeRequestIdAsync(intake.Id)).Count);
    }
}
