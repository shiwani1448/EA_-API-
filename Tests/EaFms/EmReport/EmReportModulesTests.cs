using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DelegationEntity = Jarvis5.Entities.EaFms.Delegation;

namespace Jarvis5.Tests.EaFms.EmReport;

/// <summary>GET /api/ea/em-report/modules — one row per module in the created-date cohort, reconciled with Overview and Attention.</summary>
public class EmReportModulesTests
{
    private static EaFmsDbContext Db() => new(new DbContextOptionsBuilder<EaFmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static readonly DateTime Base = new(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc);
    private static DateTime Today => IndiaBusinessCalendar.Today;

    private sealed class World
    {
        public required EaFmsDbContext Db { get; init; }
        public required Dictionary<string, BusinessModule> M { get; init; }
        public EmReportService Svc => new(Db);
    }

    /// <summary>Module ids are whatever the database assigns; nothing here assumes 4/5/6/7.</summary>
    private static async Task<World> NewWorldAsync(params string[] names)
    {
        var db = Db();
        // burn some identities so ids do not look like the developer database
        db.BusinessModules.Add(new BusinessModule { Name = "Spacer", IsActive = true, CreatedBy = "seed", CreatedDate = Base });
        var mods = (names.Length == 0 ? new[] { "Meeting", "EA Approval", "Travel & Hospitality", "Delegation" } : names)
            .Select(n => new BusinessModule { Name = n, IsActive = true, CreatedBy = "seed", CreatedDate = Base }).ToList();
        db.BusinessModules.AddRange(mods);
        await db.SaveChangesAsync();
        return new World { Db = db, M = mods.ToDictionary(x => x.Name) };
    }

    private static EaTask T(BusinessModule m, string rec, string status, DateTime? created = null) => new()
    {
        BusinessModuleId = m.Id, ModuleName = m.Name, BusinessRecordId = rec, Task = rec, ExecutionStatus = status,
        CreatedBy = "seed", CreatedDate = created ?? Base, IsActive = true
    };

    private static EaTask Completed(BusinessModule m, string rec, int? allotted = null, int? used = null)
    {
        var t = T(m, rec, EaTaskExecutionStatus.Completed);
        t.CompletedAt = DateTime.UtcNow; t.AllottedTatMinutes = allotted; t.TatUsedMinutes = used;
        return t;
    }

    private static Followup F(BusinessModule m, string rec, bool done = false, bool deleted = false) => new()
    {
        BusinessModuleId = m.Id, BusinessRecordId = rec, DueAt = Base, CompletedAt = done ? Base : null, IsDeleted = deleted, CreatedBy = "seed", CreatedDate = Base
    };

    private static Escalation E(long followupId, bool resolved = false, bool acknowledged = false, bool deleted = false) => new()
    {
        FollowupId = followupId, EscalationLevelId = 1, InitiatedAt = Base, ResolvedAt = resolved ? Base : null,
        AcknowledgedAt = acknowledged ? Base : null, IsDeleted = deleted, CreatedBy = "seed", CreatedDate = Base
    };

    /// <summary>A rich scenario across all four modules, including deadlines, pauses, follow-ups and escalations.</summary>
    private static async Task<World> RichAsync()
    {
        var w = await NewWorldAsync(); var db = w.Db;
        var meeting = w.M["Meeting"]; var approval = w.M["EA Approval"]; var travel = w.M["Travel & Hospitality"]; var deleg = w.M["Delegation"];
        var wf = new WorkflowInstance { BusinessModuleId = meeting.Id, BusinessRecordId = "m-paused", CreatedBy = "seed", CreatedDate = Base };
        db.WorkflowInstances.Add(wf); await db.SaveChangesAsync();
        db.WorkPauses.Add(new WorkPause { WorkflowInstanceId = wf.Id, StartAt = Base, CreatedBy = "seed", CreatedDate = Base });

        var paused = T(meeting, "m-paused", EaTaskExecutionStatus.InProgress); paused.WorkflowInstanceId = wf.Id;
        var lateActive = T(meeting, "m-late", EaTaskExecutionStatus.InProgress); lateActive.AllottedTatMinutes = 1; lateActive.StartedAt = DateTime.UtcNow.AddDays(-2);
        db.Tasks.AddRange(
            T(meeting, "m-new", EaTaskExecutionStatus.NotStarted), T(meeting, "m-prog", EaTaskExecutionStatus.InProgress), paused, lateActive,
            Completed(meeting, "m-on", 60, 60), Completed(meeting, "m-lateDone", 60, 90), Completed(meeting, "m-nm"),
            T(meeting, "m-x", EaTaskExecutionStatus.Cancelled),
            T(approval, "APR-1", EaTaskExecutionStatus.InProgress), T(approval, "APR-2", EaTaskExecutionStatus.Cancelled),
            T(travel, "1", EaTaskExecutionStatus.NotStarted), Completed(travel, "2"),
            T(deleg, "10", EaTaskExecutionStatus.InProgress), Completed(deleg, "11"), Completed(deleg, "12"));

        db.ApprovalRequests.Add(new ApprovalRequest { ReferenceNo = "APR-1", EaTaskId = 1, CreatedBy = "s", RequiredApprovalDate = Today.AddDays(-3) });      // active delayed
        db.TravelRequests.Add(new TravelRequest { Id = 1, ReferenceNo = "TRV-1", EaTaskId = 1, CreatedBy = "s", RequiredDate = Today.AddDays(9) });
        db.TravelRequests.Add(new TravelRequest { Id = 2, ReferenceNo = "TRV-2", EaTaskId = 2, CreatedBy = "s", RequiredDate = Today.AddDays(9) });               // completed on time
        db.Delegations.Add(new DelegationEntity { Id = 10, ReferenceNo = "D-10", EaTaskId = 1, Title = "d", DoerId = "a", AssignedById = "b", CreatedBy = "s", DueDate = Today.AddDays(-4) });   // active delayed
        db.Delegations.Add(new DelegationEntity { Id = 11, ReferenceNo = "D-11", EaTaskId = 1, Title = "d", DoerId = "a", AssignedById = "b", CreatedBy = "s", DueDate = Today.AddDays(-30) }); // completed late
        db.Delegations.Add(new DelegationEntity { Id = 12, ReferenceNo = "D-12", EaTaskId = 1, Title = "d", DoerId = "a", AssignedById = "b", CreatedBy = "s", DueDate = Today.AddDays(30) });  // completed on time

        var f1 = F(meeting, "m-new"); var f2 = F(meeting, "m-new"); var fDone = F(meeting, "m-prog", done: true);
        var fDel = F(meeting, "m-x", deleted: true); var fApproval = F(approval, "APR-1"); var fDeleg = F(deleg, "10");
        var fDeletedEsc = F(deleg, "11");
        db.Followups.AddRange(f1, f2, fDone, fDel, fApproval, fDeleg, fDeletedEsc);
        await db.SaveChangesAsync();
        db.Escalations.AddRange(
            E(f1.Id), E(f1.Id, acknowledged: true), E(f2.Id, resolved: true), E(fDone.Id),      // meeting: 3 unresolved (f1 x2 + fDone), 1 resolved
            E(fDel.Id),                                                                          // parent followup deleted -> excluded
            E(fDeleg.Id), E(fDeletedEsc.Id, deleted: true));                                     // delegation: 1 open, 1 deleted
        await db.SaveChangesAsync();
        return w;
    }

    private static EmReportModuleSummaryDto Row(IReadOnlyList<EmReportModuleSummaryDto> rows, BusinessModule m) => Assert.Single(rows, r => r.BusinessModuleId == m.Id);

    // ---------------- grouping / population ----------------
    [Fact]
    public async Task EachModuleGetsOneRow_GroupedByBusinessModuleId_WithoutHardcodedIds()
    {
        var w = await RichAsync(); await using var _ = w.Db;

        var rows = await w.Svc.GetModulesAsync(new(), default);

        Assert.Equal(4, rows.Count);
        Assert.Equal(new[] { "Meeting", "EA Approval", "Travel & Hospitality", "Delegation" }.Select(n => w.M[n].Id).OrderBy(x => x), rows.Select(r => r.BusinessModuleId));
        Assert.Equal(8, Row(rows, w.M["Meeting"]).TotalTasks);
        Assert.Equal(2, Row(rows, w.M["EA Approval"]).TotalTasks);
        Assert.Equal(2, Row(rows, w.M["Travel & Hospitality"]).TotalTasks);
        Assert.Equal(3, Row(rows, w.M["Delegation"]).TotalTasks);
        Assert.DoesNotContain(rows, r => r.ModuleName == "Spacer");                 // zero-task module omitted
        Assert.All(new[] { 4L, 5L, 6L, 7L }, id => Assert.NotEqual(id, w.M["Meeting"].Id));
    }

    [Fact]
    public async Task ModuleName_ComesFromTheCurrentMaster_AndDeactivatedModulesStayVisible()
    {
        var w = await NewWorldAsync("Vendor Management", "Retired"); await using var _ = w.Db;
        var vendor = w.M["Vendor Management"]; var retired = w.M["Retired"];
        w.Db.Tasks.Add(T(vendor, "1", EaTaskExecutionStatus.NotStarted)); w.Db.Tasks.Add(T(retired, "2", EaTaskExecutionStatus.NotStarted));
        await w.Db.SaveChangesAsync();
        vendor.Name = "Vendor Mgmt (renamed)"; retired.IsActive = false;                      // EaTask.ModuleName snapshot is now stale
        await w.Db.SaveChangesAsync();

        var rows = await w.Svc.GetModulesAsync(new(), default);

        Assert.Equal("Vendor Mgmt (renamed)", Row(rows, vendor).ModuleName);
        Assert.Equal("Retired", Row(rows, retired).ModuleName);                               // inactive: still reported
        Assert.Equal("Vendor Management", (await w.Db.Tasks.AsNoTracking().FirstAsync(t => t.BusinessModuleId == vendor.Id)).ModuleName);   // snapshot untouched
    }

    [Fact]
    public async Task BusinessModuleFilter_ReturnsOnlyThatModule_AndAnEmptyListWhenItHasNoCohortTasks()
    {
        var w = await RichAsync(); await using var _ = w.Db;
        var spacerId = (await w.Db.BusinessModules.FirstAsync(m => m.Name == "Spacer")).Id;

        var one = await w.Svc.GetModulesAsync(new() { BusinessModuleId = w.M["Delegation"].Id }, default);
        var none = await w.Svc.GetModulesAsync(new() { BusinessModuleId = spacerId }, default);
        var outOfRange = await w.Svc.GetModulesAsync(new() { FromDate = new DateTime(2030, 1, 1) }, default);

        Assert.Equal(w.M["Delegation"].Id, Assert.Single(one).BusinessModuleId);
        Assert.Empty(none);
        Assert.Empty(outOfRange);
    }

    [Fact]
    public async Task InvalidFilters_AreRejectedLikeOverviewAndAttention()
    {
        var w = await NewWorldAsync(); await using var _ = w.Db;
        var bad = new EmReportOverviewQueryDto { FromDate = new DateTime(2026, 9, 11), ToDate = new DateTime(2026, 9, 10) };

        await Assert.ThrowsAsync<BadRequestException>(() => w.Svc.GetModulesAsync(bad, default));
        await Assert.ThrowsAsync<BadRequestException>(() => w.Svc.GetOverviewAsync(bad, default));
        await Assert.ThrowsAsync<BadRequestException>(() => w.Svc.GetAttentionAsync(bad, default));
        await Assert.ThrowsAsync<BadRequestException>(() => w.Svc.GetModulesAsync(new() { BusinessModuleId = 0 }, default));
    }

    // ---------------- status / percentages ----------------
    [Fact]
    public async Task StatusBuckets_AreMutuallyExclusive_AndPausedIsNotAlsoInProgress()
    {
        var w = await RichAsync(); await using var _ = w.Db;

        var m = Row(await w.Svc.GetModulesAsync(new(), default), w.M["Meeting"]);

        Assert.Equal((8, 1, 2, 1, 3, 1), (m.TotalTasks, m.NotStarted, m.InProgress, m.Paused, m.Completed, m.Cancelled));
        Assert.Equal(m.TotalTasks, m.NotStarted + m.InProgress + m.Paused + m.Completed + m.Cancelled);
        Assert.Equal(decimal.Round(3 * 100m / 7, 2), m.CompletionPercentage);                  // completed / (total - cancelled)
    }

    [Fact]
    public async Task ZeroDenominators_YieldZeroPercentages()
    {
        var w = await NewWorldAsync("Vendor"); await using var _ = w.Db;
        w.Db.Tasks.Add(T(w.M["Vendor"], "1", EaTaskExecutionStatus.Cancelled)); await w.Db.SaveChangesAsync();

        var r = Assert.Single(await w.Svc.GetModulesAsync(new(), default));

        Assert.Equal((0m, 0m, 0), (r.CompletionPercentage, r.OnTimeCompletionPercentage, r.Delayed));
    }

    // ---------------- performance ----------------
    [Fact]
    public async Task Performance_OnTimeDelayedNotMeasured_AndDelayedCombinesActiveAndCompletedLate()
    {
        var w = await RichAsync(); await using var _ = w.Db;
        var rows = await w.Svc.GetModulesAsync(new(), default);

        var meeting = Row(rows, w.M["Meeting"]);
        Assert.Equal((1, 1, 1), (meeting.OnTimeCompleted, meeting.DelayedCompleted, meeting.NotMeasuredCompleted));
        Assert.Equal(50m, meeting.OnTimeCompletionPercentage);                                 // not-measured excluded from the denominator
        Assert.Equal(2, meeting.Delayed);                                                      // active delayed + completed late; cancelled never

        var approval = Row(rows, w.M["EA Approval"]);
        Assert.Equal(1, approval.Delayed);                                                     // active, RequiredApprovalDate passed
        Assert.Equal((1, 0), (approval.Cancelled, approval.OnTimeCompleted));

        var travel = Row(rows, w.M["Travel & Hospitality"]);
        Assert.Equal((1, 0, 0, 100m, 0), (travel.OnTimeCompleted, travel.DelayedCompleted, travel.NotMeasuredCompleted, travel.OnTimeCompletionPercentage, travel.Delayed));

        var deleg = Row(rows, w.M["Delegation"]);
        Assert.Equal((1, 1, 0, 50m, 2), (deleg.OnTimeCompleted, deleg.DelayedCompleted, deleg.NotMeasuredCompleted, deleg.OnTimeCompletionPercentage, deleg.Delayed));
    }

    // ---------------- follow-up / escalation ----------------
    [Fact]
    public async Task FollowupsCountDistinctTasks_EscalationsCountRows_PerSourceModule()
    {
        var w = await RichAsync(); await using var _ = w.Db;
        var rows = await w.Svc.GetModulesAsync(new(), default);

        var meeting = Row(rows, w.M["Meeting"]);
        Assert.Equal(1, meeting.TasksRequiringFollowup);          // two pending followups on one task = 1; completed-only and deleted = 0
        Assert.Equal(3, meeting.OpenEscalations);                 // unresolved + acknowledged-unresolved rows; resolved and deleted-parent excluded
        Assert.Equal((1, 0), (Row(rows, w.M["EA Approval"]).TasksRequiringFollowup, Row(rows, w.M["EA Approval"]).OpenEscalations));
        Assert.Equal((0, 0), (Row(rows, w.M["Travel & Hospitality"]).TasksRequiringFollowup, Row(rows, w.M["Travel & Hospitality"]).OpenEscalations));
        Assert.Equal((2, 1), (Row(rows, w.M["Delegation"]).TasksRequiringFollowup, Row(rows, w.M["Delegation"]).OpenEscalations));   // two tasks (10 and 11) have pending follow-ups; one open escalation row
    }

    [Fact]
    public async Task FollowupOnACompletedOrCancelledParent_IsStillCounted_LikeAttention()
    {
        var w = await NewWorldAsync("Vendor"); await using var _ = w.Db;
        w.Db.Tasks.AddRange(Completed(w.M["Vendor"], "done"), T(w.M["Vendor"], "gone", EaTaskExecutionStatus.Cancelled));
        w.Db.Followups.AddRange(F(w.M["Vendor"], "done"), F(w.M["Vendor"], "gone"), F(w.M["Vendor"], "nobody-has-this-task"));
        await w.Db.SaveChangesAsync();

        var r = Assert.Single(await w.Svc.GetModulesAsync(new(), default));

        Assert.Equal(2, r.TasksRequiringFollowup);                 // follow-up on a task outside the cohort is ignored
        Assert.Equal((await w.Svc.GetAttentionAsync(new(), default)).TasksRequiringFollowup, r.TasksRequiringFollowup);
    }

    // ---------------- reconciliation ----------------
    private static async Task AssertReconcilesAsync(World w, EmReportOverviewQueryDto q)
    {
        var rows = await w.Svc.GetModulesAsync(q, default);
        var o = await w.Svc.GetOverviewAsync(q, default);
        var a = await w.Svc.GetAttentionAsync(q, default);

        Assert.Equal(o.TotalTasks, rows.Sum(r => r.TotalTasks));
        Assert.Equal(o.NotStarted, rows.Sum(r => r.NotStarted));
        Assert.Equal(o.InProgress, rows.Sum(r => r.InProgress));
        Assert.Equal(o.Paused, rows.Sum(r => r.Paused));
        Assert.Equal(o.Completed, rows.Sum(r => r.Completed));
        Assert.Equal(o.Cancelled, rows.Sum(r => r.Cancelled));
        Assert.Equal(o.OnTimeCompleted, rows.Sum(r => r.OnTimeCompleted));
        Assert.Equal(o.DelayedCompleted, rows.Sum(r => r.DelayedCompleted));
        Assert.Equal(o.NotMeasuredCompleted, rows.Sum(r => r.NotMeasuredCompleted));
        Assert.Equal(o.DelayedTasks, rows.Sum(r => r.Delayed));
        Assert.Equal(a.Delayed, rows.Sum(r => r.Delayed));
        Assert.Equal(a.TasksRequiringFollowup, rows.Sum(r => r.TasksRequiringFollowup));
        Assert.Equal(a.OpenEscalations, rows.Sum(r => r.OpenEscalations));
    }

    [Fact]
    public async Task ModulesReconcileWithOverviewAndAttention_Unfiltered()
    {
        var w = await RichAsync(); await using var _ = w.Db;
        await AssertReconcilesAsync(w, new());
        Assert.True((await w.Svc.GetOverviewAsync(new(), default)).TotalTasks > 0);
    }

    [Fact]
    public async Task ModulesReconcile_WithADateFilter_ThatExcludesSomeTasks()
    {
        var w = await RichAsync(); await using var _ = w.Db;
        var old = T(w.M["Meeting"], "m-old", EaTaskExecutionStatus.InProgress, Base.AddDays(-20));
        w.Db.Tasks.Add(old); w.Db.Followups.Add(F(w.M["Meeting"], "m-old")); await w.Db.SaveChangesAsync();
        var q = new EmReportOverviewQueryDto { FromDate = new DateTime(2026, 9, 10), ToDate = new DateTime(2026, 9, 10) };

        await AssertReconcilesAsync(w, q);

        var all = (await w.Svc.GetOverviewAsync(new(), default)).TotalTasks;
        var filtered = (await w.Svc.GetOverviewAsync(q, default)).TotalTasks;
        Assert.True(filtered < all);
    }

    [Fact]
    public async Task ModulesReconcile_WithABusinessModuleFilter_ForEveryModule()
    {
        var w = await RichAsync(); await using var _ = w.Db;
        foreach (var m in w.M.Values)
        {
            await AssertReconcilesAsync(w, new() { BusinessModuleId = m.Id });
            var overview = await w.Svc.GetOverviewAsync(new() { BusinessModuleId = m.Id }, default);
            var full = Row(await w.Svc.GetModulesAsync(new(), default), m);
            Assert.Equal(full.TotalTasks, overview.TotalTasks);                                // filtered overview == that module's row in the full list
        }
    }

    [Fact]
    public async Task OverviewAndAttention_AreUnchangedByTheSharedContextRefactor()
    {
        var w = await RichAsync(); await using var _ = w.Db;

        var o = await w.Svc.GetOverviewAsync(new(), default);
        var a = await w.Svc.GetAttentionAsync(new(), default);

        Assert.Equal((15, 2, 4, 1, 6, 2), (o.TotalTasks, o.NotStarted, o.InProgress, o.Paused, o.Completed, o.Cancelled));
        Assert.Equal((3, 2, 1), (o.OnTimeCompleted, o.DelayedCompleted, o.NotMeasuredCompleted));
        Assert.Equal(5, o.DelayedTasks);
        Assert.Equal((5, 4, 4), (a.Delayed, a.TasksRequiringFollowup, a.OpenEscalations));
    }

    // ---------------- read-only ----------------
    [Fact]
    public async Task ModulesEndpoint_WritesNothing()
    {
        var w = await RichAsync(); await using var _ = w.Db;
        var before = (await w.Db.Tasks.CountAsync(), await w.Db.Followups.CountAsync(), await w.Db.Escalations.CountAsync(),
            await w.Db.AuditLogs.CountAsync(), await w.Db.Notifications.CountAsync(), await w.Db.BusinessModules.CountAsync());

        await w.Svc.GetModulesAsync(new(), default);

        Assert.False(w.Db.ChangeTracker.HasChanges());
        Assert.Equal(before, (await w.Db.Tasks.CountAsync(), await w.Db.Followups.CountAsync(), await w.Db.Escalations.CountAsync(),
            await w.Db.AuditLogs.CountAsync(), await w.Db.Notifications.CountAsync(), await w.Db.BusinessModules.CountAsync()));
    }
}
