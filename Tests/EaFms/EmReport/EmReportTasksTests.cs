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

/// <summary>GET /api/ea/em-report/tasks — paginated drill-down, one row per EaTask, reconciled with the other EM endpoints.</summary>
public class EmReportTasksTests
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

    private static async Task<World> NewWorldAsync(params string[] names)
    {
        var db = Db();
        db.BusinessModules.Add(new BusinessModule { Name = "Spacer", IsActive = true, CreatedBy = "seed", CreatedDate = Base });
        var mods = (names.Length == 0 ? new[] { "Meeting", "EA Approval", "Travel & Hospitality", "Delegation" } : names)
            .Select(n => new BusinessModule { Name = n, IsActive = true, CreatedBy = "seed", CreatedDate = Base }).ToList();
        db.BusinessModules.AddRange(mods);
        await db.SaveChangesAsync();
        return new World { Db = db, M = mods.ToDictionary(x => x.Name) };
    }

    private static EaTask T(BusinessModule m, string rec, string status, DateTime? created = null) => new()
    {
        BusinessModuleId = m.Id, ModuleName = m.Name, BusinessRecordId = rec, Task = "Task " + rec, ExecutionStatus = status,
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

    /// <summary>Same cross-module scenario as the module summary tests, plus a delayed Travel task and a cancelled task with a Followup.</summary>
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
            T(travel, "1", EaTaskExecutionStatus.NotStarted), Completed(travel, "2"), T(travel, "3", EaTaskExecutionStatus.InProgress),
            T(deleg, "10", EaTaskExecutionStatus.InProgress), Completed(deleg, "11"), Completed(deleg, "12"));

        db.ApprovalRequests.Add(new ApprovalRequest { ReferenceNo = "APR-1", EaTaskId = 1, CreatedBy = "s", RequiredApprovalDate = Today.AddDays(-3) });
        db.TravelRequests.Add(new TravelRequest { Id = 1, ReferenceNo = "TRV-1", EaTaskId = 1, CreatedBy = "s", RequiredDate = Today.AddDays(9) });
        db.TravelRequests.Add(new TravelRequest { Id = 2, ReferenceNo = "TRV-2", EaTaskId = 2, CreatedBy = "s", RequiredDate = Today.AddDays(9) });
        db.TravelRequests.Add(new TravelRequest { Id = 3, ReferenceNo = "TRV-3", EaTaskId = 3, CreatedBy = "s", RequiredDate = Today.AddDays(-2) });   // active, past required date
        db.Delegations.Add(new DelegationEntity { Id = 10, ReferenceNo = "D-10", EaTaskId = 1, Title = "d", DoerId = "a", AssignedById = "b", CreatedBy = "s", DueDate = Today.AddDays(-4) });
        db.Delegations.Add(new DelegationEntity { Id = 11, ReferenceNo = "D-11", EaTaskId = 1, Title = "d", DoerId = "a", AssignedById = "b", CreatedBy = "s", DueDate = Today.AddDays(-30) });
        db.Delegations.Add(new DelegationEntity { Id = 12, ReferenceNo = "D-12", EaTaskId = 1, Title = "d", DoerId = "a", AssignedById = "b", CreatedBy = "s", DueDate = Today.AddDays(30) });

        var f1 = F(meeting, "m-new"); var f2 = F(meeting, "m-new"); var f3done = F(meeting, "m-new", done: true);
        var fDone = F(meeting, "m-prog", done: true); var fDel = F(meeting, "m-x", deleted: true);
        var fApproval = F(approval, "APR-1"); var fCancelled = F(approval, "APR-2"); var fDeleg = F(deleg, "10"); var fCompletedParent = F(deleg, "11");
        db.Followups.AddRange(f1, f2, f3done, fDone, fDel, fApproval, fCancelled, fDeleg, fCompletedParent);
        await db.SaveChangesAsync();
        db.Escalations.AddRange(
            E(f1.Id), E(f1.Id, acknowledged: true), E(f2.Id, resolved: true),   // m-new: unresolved + acknowledged = 2, resolved excluded
            E(fDone.Id),                                                          // m-prog: 1 (open escalation on a completed followup still counts)
            E(fDel.Id),                                                           // parent followup deleted -> excluded
            E(fDeleg.Id), E(fCompletedParent.Id, deleted: true));                 // d-10: 1; d-11: deleted -> 0
        await db.SaveChangesAsync();
        return w;
    }

    private static Task<Jarvis5.Common.PagedResult<EmReportTaskRowDto>> Get(World w, Action<EmReportTaskRegisterQueryDto>? tweak = null)
    {
        var q = new EmReportTaskRegisterQueryDto { PageSize = 200 };
        tweak?.Invoke(q);
        return w.Svc.GetTasksAsync(q, default);
    }

    private static async Task<EmReportTaskRowDto> One(World w, string record) =>
        Assert.Single((await Get(w)).Items, r => r.BusinessRecordId == record);

    // ---------------- basic row ----------------
    [Fact]
    public async Task Row_ExposesIdentityStatusPerformanceFollowupEscalationAndTat()
    {
        var w = await RichAsync(); await using var _ = w.Db;
        var task = await w.Db.Tasks.AsNoTracking().SingleAsync(t => t.BusinessRecordId == "m-on");

        var r = await One(w, "m-on");

        Assert.Equal((task.Id, w.M["Meeting"].Id, "Meeting", "m-on", "Task m-on"), (r.EaTaskId, r.BusinessModuleId, r.ModuleName, r.BusinessRecordId, r.Task));
        Assert.Equal(("Completed", false, null, "OnTime"), (r.ExecutionStatus, r.IsPaused, r.DueDate, r.Performance));
        Assert.Equal(("NoFollowup", 0, 0), (r.FollowupStatus, r.PendingFollowupCount, r.OpenEscalationCount));
        Assert.Equal((60, 60), (r.AllottedTatMinutes, r.CurrentOrFinalTatUsedMinutes));
    }

    [Fact]
    public async Task ModuleName_IsTheCurrentMasterName_AndBusinessRecordIdIsNotReplacedByTheTaskId()
    {
        var w = await NewWorldAsync("Vendor"); await using var _ = w.Db;
        w.Db.Tasks.Add(T(w.M["Vendor"], "APR-2026-000012", EaTaskExecutionStatus.NotStarted)); await w.Db.SaveChangesAsync();
        w.M["Vendor"].Name = "Vendor (renamed)"; await w.Db.SaveChangesAsync();

        var r = Assert.Single((await Get(w)).Items);

        Assert.Equal("Vendor (renamed)", r.ModuleName);
        Assert.Equal("APR-2026-000012", r.BusinessRecordId);
        Assert.NotEqual(r.EaTaskId.ToString(), r.BusinessRecordId);
        Assert.Equal("Vendor", (await w.Db.Tasks.AsNoTracking().SingleAsync()).ModuleName);   // snapshot untouched
    }

    // ---------------- status ----------------
    [Theory]
    [InlineData("m-new", "NotStarted", false)]
    [InlineData("m-prog", "InProgress", false)]
    [InlineData("m-paused", "InProgress", true)]       // Paused stays InProgress + isPaused
    [InlineData("m-on", "Completed", false)]
    [InlineData("m-x", "Cancelled", false)]
    public async Task ExecutionStatus_IsPersistedValue_AndPausedIsDerived(string record, string status, bool paused)
    {
        var w = await RichAsync(); await using var _ = w.Db;

        var r = await One(w, record);

        Assert.Equal((status, paused), (r.ExecutionStatus, r.IsPaused));
        Assert.DoesNotContain("Paused", (await Get(w)).Items.Select(x => x.ExecutionStatus));
    }

    // ---------------- performance / due date / TAT ----------------
    [Theory]
    [InlineData("m-on", "OnTime")]
    [InlineData("m-lateDone", "Delayed")]
    [InlineData("m-late", "Delayed")]
    [InlineData("m-nm", "NotMeasured")]
    [InlineData("m-paused", "NotMeasured")]     // no TAT, Meeting has no other deadline
    [InlineData("APR-1", "Delayed")]            // RequiredApprovalDate passed, still open
    [InlineData("1", "OnTime")]                 // Travel, RequiredDate in the future (module travel)
    [InlineData("3", "Delayed")]                // Travel RequiredDate passed
    [InlineData("10", "Delayed")]               // Delegation DueDate passed (active)
    [InlineData("11", "Delayed")]               // Delegation completed after DueDate
    [InlineData("12", "OnTime")]                // Delegation completed before DueDate
    [InlineData("m-x", "NotMeasured")]          // Cancelled is never measured
    [InlineData("APR-2", "NotMeasured")]
    public async Task Performance_UsesTheSharedClassifier(string record, string expected)
    {
        var w = await RichAsync(); await using var _ = w.Db;

        var r = (await Get(w)).Items.First(x => x.BusinessRecordId == record);

        Assert.Equal(expected, r.Performance);
    }

    [Fact]
    public async Task TaskWithoutADeadlineSource_IsNotMeasured()
    {
        var w = await NewWorldAsync("Delegation"); await using var _ = w.Db;
        w.Db.Tasks.Add(T(w.M["Delegation"], "999", EaTaskExecutionStatus.InProgress));   // no Delegation row -> no deadline
        await w.Db.SaveChangesAsync();

        var r = Assert.Single((await Get(w)).Items);

        Assert.Equal(("NotMeasured", null), (r.Performance, r.DueDate));
    }

    [Fact]
    public async Task DueDate_IsOnlyTheRealTaskDeadline_NullForMeeting()
    {
        var w = await RichAsync(); await using var _ = w.Db;
        var items = (await Get(w)).Items;

        Assert.Null(items.Single(x => x.BusinessRecordId == "m-late").DueDate);                       // never TAT expiry
        Assert.Equal(Today.AddDays(-3), items.Single(x => x.BusinessRecordId == "APR-1").DueDate);   // Approval
        Assert.Equal(Today.AddDays(9), items.Single(x => x.BusinessRecordId == "1").DueDate);        // Travel
        Assert.Equal(Today.AddDays(-4), items.Single(x => x.BusinessRecordId == "10").DueDate);      // Delegation
        Assert.Null(items.Single(x => x.BusinessRecordId == "m-nm").DueDate);
    }

    [Fact]
    public async Task Tat_FrozenWhenCompleted_CurrentWhenActive_NullWhenNotApplicable()
    {
        var w = await RichAsync(); await using var _ = w.Db;

        var completed = await One(w, "m-lateDone");
        var active = await One(w, "m-late");
        var noTat = await One(w, "m-nm");
        var approval = await One(w, "APR-1");
        var cancelled = await One(w, "m-x");

        Assert.Equal((60, 90), (completed.AllottedTatMinutes, completed.CurrentOrFinalTatUsedMinutes));   // frozen value
        Assert.Equal(1, active.AllottedTatMinutes);
        Assert.True(active.CurrentOrFinalTatUsedMinutes > 1);                                              // canonical live value (2 days elapsed)
        Assert.Equal((null, null), (noTat.AllottedTatMinutes, noTat.CurrentOrFinalTatUsedMinutes));        // null, never 0
        Assert.Equal((null, null), (approval.AllottedTatMinutes, approval.CurrentOrFinalTatUsedMinutes));
        Assert.Null(cancelled.CurrentOrFinalTatUsedMinutes);
    }

    // ---------------- follow-up / escalation ----------------
    [Theory]
    [InlineData("m-x", "NoFollowup", 0)]        // only a deleted Followup
    [InlineData("m-nm", "NoFollowup", 0)]
    [InlineData("m-prog", "Completed", 0)]      // completed Followup only: does not require follow-up
    [InlineData("m-new", "Pending", 2)]         // two incomplete + one completed
    [InlineData("APR-1", "Pending", 1)]
    [InlineData("11", "Pending", 1)]            // completed parent task, incomplete Followup
    [InlineData("APR-2", "Pending", 1)]         // cancelled parent task follows the same (Attention) architecture
    public async Task FollowupStatusAndPendingCount(string record, string status, int pending)
    {
        var w = await RichAsync(); await using var _ = w.Db;

        var r = (await Get(w)).Items.First(x => x.BusinessRecordId == record);

        Assert.Equal((status, pending), (r.FollowupStatus, r.PendingFollowupCount));
    }

    [Theory]
    [InlineData("m-nm", 0)]
    [InlineData("m-new", 2)]        // unresolved + acknowledged-unresolved; the resolved one is excluded
    [InlineData("m-prog", 1)]
    [InlineData("m-x", 0)]          // escalation under a deleted Followup
    [InlineData("10", 1)]
    [InlineData("11", 0)]           // deleted escalation
    public async Task OpenEscalationCount(string record, int expected)
    {
        var w = await RichAsync(); await using var _ = w.Db;

        Assert.Equal(expected, (await Get(w)).Items.First(x => x.BusinessRecordId == record).OpenEscalationCount);
    }

    // ---------------- filters ----------------
    [Fact]
    public async Task DateAndModuleFilters_MatchTheOtherEmEndpointsCohort()
    {
        var w = await RichAsync(); await using var _ = w.Db;
        w.Db.Tasks.Add(T(w.M["Meeting"], "m-old", EaTaskExecutionStatus.NotStarted, Base.AddDays(-20))); await w.Db.SaveChangesAsync();

        var all = await Get(w);
        var from = await Get(w, q => q.FromDate = new DateTime(2026, 9, 10));
        var to = await Get(w, q => q.ToDate = new DateTime(2026, 9, 9));
        var module = await Get(w, q => q.BusinessModuleId = w.M["Delegation"].Id);

        Assert.Equal(all.TotalCount - 1, from.TotalCount);
        Assert.Equal("m-old", Assert.Single(to.Items).BusinessRecordId);
        Assert.Equal(3, module.TotalCount);
        Assert.All(module.Items, r => Assert.Equal(w.M["Delegation"].Id, r.BusinessModuleId));
    }

    [Theory]
    [InlineData("NotStarted", 2)]
    [InlineData("InProgress", 5)]      // m-prog, m-late, APR-1, travel 3, deleg 10 (paused excluded)
    [InlineData("Paused", 1)]
    [InlineData("Completed", 6)]
    [InlineData("Cancelled", 2)]
    [InlineData("paused", 1)]          // case-insensitive
    public async Task ExecutionStatusFilter_UsesDisplayStates(string status, int expected)
    {
        var w = await RichAsync(); await using var _ = w.Db;

        var page = await Get(w, q => q.ExecutionStatus = status);

        Assert.Equal(expected, page.TotalCount);
        if (status.Equals("Paused", StringComparison.OrdinalIgnoreCase)) Assert.All(page.Items, r => Assert.True(r.IsPaused && r.ExecutionStatus == "InProgress"));
        if (status == "InProgress") Assert.All(page.Items, r => Assert.False(r.IsPaused));
    }

    [Theory]
    [InlineData("Overdue")]
    [InlineData("Resumed")]
    [InlineData("nonsense")]
    public async Task InvalidExecutionStatus_Is400(string status)
    {
        var w = await RichAsync(); await using var _ = w.Db;

        await Assert.ThrowsAsync<BadRequestException>(() => Get(w, q => q.ExecutionStatus = status));
    }

    [Theory]
    [InlineData("OnTime", 4)]          // m-on, travel 1, travel 2, delegation 12
    [InlineData("Delayed", 6)]         // m-late, m-lateDone, APR-1, travel 3, delegation 10, 11
    [InlineData("NotMeasured", 6)]     // m-new, m-prog, m-paused, m-nm, and the two cancelled tasks
    [InlineData("delayed", 6)]
    public async Task PerformanceFilter_UsesTheSharedClassification(string performance, int expected)
    {
        var w = await RichAsync(); await using var _ = w.Db;

        var page = await Get(w, q => q.Performance = performance);

        Assert.Equal(expected, page.TotalCount);
        Assert.All(page.Items, r => Assert.Equal(performance, r.Performance, ignoreCase: true));
    }

    [Theory]
    [InlineData("Overdue")]
    [InlineData("TatBreached")]
    [InlineData("Late")]
    [InlineData("1")]
    public async Task InvalidPerformance_Is400(string performance)
    {
        var w = await RichAsync(); await using var _ = w.Db;

        await Assert.ThrowsAsync<BadRequestException>(() => Get(w, q => q.Performance = performance));
    }

    [Fact]
    public async Task RequiresFollowupFilter_TrueIsPending_FalseIncludesNoFollowupAndCompletedOnly()
    {
        var w = await RichAsync(); await using var _ = w.Db;

        var yes = await Get(w, q => q.RequiresFollowup = true);
        var no = await Get(w, q => q.RequiresFollowup = false);

        Assert.All(yes.Items, r => Assert.Equal("Pending", r.FollowupStatus));
        Assert.Equal(5, yes.TotalCount);                            // m-new, APR-1, APR-2, deleg 10, deleg 11
        Assert.Contains(no.Items, r => r.FollowupStatus == "NoFollowup");
        Assert.Contains(no.Items, r => r.FollowupStatus == "Completed");
        Assert.Equal(yes.TotalCount + no.TotalCount, (await Get(w)).TotalCount);
    }

    [Fact]
    public async Task HasOpenEscalationFilter()
    {
        var w = await RichAsync(); await using var _ = w.Db;

        var yes = await Get(w, q => q.HasOpenEscalation = true);
        var no = await Get(w, q => q.HasOpenEscalation = false);

        Assert.Equal(new[] { "10", "m-new", "m-prog" }, yes.Items.Select(r => r.BusinessRecordId).OrderBy(x => x));
        Assert.All(yes.Items, r => Assert.True(r.OpenEscalationCount > 0));
        Assert.All(no.Items, r => Assert.Equal(0, r.OpenEscalationCount));
        Assert.Equal((await Get(w)).TotalCount, yes.TotalCount + no.TotalCount);
    }

    [Fact]
    public async Task Search_MatchesTaskRecordIdAndCurrentModuleName_TrimsAndIgnoresBlank()
    {
        var w = await RichAsync(); await using var _ = w.Db;
        var all = (await Get(w)).TotalCount;

        Assert.Equal("m-lateDone", Assert.Single((await Get(w, q => q.Search = "  TASK M-LATEDONE ")).Items).BusinessRecordId);   // task title, case-insensitive
        Assert.Equal("APR-1", Assert.Single((await Get(w, q => q.Search = "apr-1")).Items).BusinessRecordId);                       // record id
        Assert.Equal(3, (await Get(w, q => q.Search = "delegation")).TotalCount);                                                    // module name
        Assert.Equal(all, (await Get(w, q => q.Search = "   ")).TotalCount);
        Assert.Equal(all, (await Get(w, q => q.Search = null)).TotalCount);
        Assert.Equal(0, (await Get(w, q => q.Search = "zzz-none")).TotalCount);
    }

    [Fact]
    public async Task InvalidCohortFilters_AreRejectedLikeTheOtherEmEndpoints()
    {
        var w = await NewWorldAsync(); await using var _ = w.Db;

        await Assert.ThrowsAsync<BadRequestException>(() => Get(w, q => { q.FromDate = new DateTime(2026, 9, 11); q.ToDate = new DateTime(2026, 9, 10); }));
        await Assert.ThrowsAsync<BadRequestException>(() => Get(w, q => q.BusinessModuleId = 0));
    }

    // ---------------- pagination ----------------
    private static async Task<World> ManyAsync()
    {
        var w = await NewWorldAsync("Vendor"); var v = w.M["Vendor"];
        for (var i = 1; i <= 25; i++)
        {
            var t = T(v, $"r{i:D2}", i % 5 == 0 ? EaTaskExecutionStatus.Completed : EaTaskExecutionStatus.NotStarted, Base.AddMinutes(i / 2));   // pairs share a timestamp
            if (t.ExecutionStatus == EaTaskExecutionStatus.Completed) t.CompletedAt = DateTime.UtcNow;
            w.Db.Tasks.Add(t);
        }
        await w.Db.SaveChangesAsync();
        return w;
    }

    [Fact]
    public async Task Pagination_PagesAreDeterministic_NonOverlapping_AndTotalIsTheFilteredTotal()
    {
        var w = await ManyAsync(); await using var _ = w.Db;

        var p1 = await w.Svc.GetTasksAsync(new() { Page = 1, PageSize = 10 }, default);
        var p2 = await w.Svc.GetTasksAsync(new() { Page = 2, PageSize = 10 }, default);
        var p3 = await w.Svc.GetTasksAsync(new() { Page = 3, PageSize = 10 }, default);
        var p4 = await w.Svc.GetTasksAsync(new() { Page = 4, PageSize = 10 }, default);
        var again = await w.Svc.GetTasksAsync(new() { Page = 2, PageSize = 10 }, default);

        Assert.Equal((10, 10, 5, 0), (p1.Items.Count, p2.Items.Count, p3.Items.Count, p4.Items.Count));
        Assert.All(new[] { p1, p2, p3, p4 }, p => Assert.Equal(25, p.TotalCount));
        Assert.Equal(3, p1.TotalPages);
        Assert.Equal(p2.Items.Select(x => x.EaTaskId), again.Items.Select(x => x.EaTaskId));
        var ids = p1.Items.Concat(p2.Items).Concat(p3.Items).Select(x => x.EaTaskId).ToList();
        Assert.Equal(25, ids.Distinct().Count());
        var ordered = p1.Items.Concat(p2.Items).Concat(p3.Items).ToList();
        Assert.Equal(ordered.Select(x => x.EaTaskId), (await Get(w)).Items.Select(x => x.EaTaskId));   // same order however it is paged
    }

    [Fact]
    public async Task Ordering_IsCreatedDateDescending_ThenIdDescending()
    {
        var w = await ManyAsync(); await using var _ = w.Db;
        var rows = (await Get(w)).Items;
        var tasks = await w.Db.Tasks.AsNoTracking().ToDictionaryAsync(t => t.Id);

        for (var i = 1; i < rows.Count; i++)
        {
            var a = tasks[rows[i - 1].EaTaskId]; var b = tasks[rows[i].EaTaskId];
            Assert.True(a.CreatedDate > b.CreatedDate || (a.CreatedDate == b.CreatedDate && a.Id > b.Id));
        }
    }

    [Fact]
    public async Task Filters_AreAppliedBeforePaging_SoTotalsAndPagesAreCorrect()
    {
        var w = await ManyAsync(); await using var _ = w.Db;

        var p1 = await w.Svc.GetTasksAsync(new() { ExecutionStatus = "Completed", Page = 1, PageSize = 3 }, default);
        var p2 = await w.Svc.GetTasksAsync(new() { ExecutionStatus = "Completed", Page = 2, PageSize = 3 }, default);

        Assert.Equal((5, 3, 2), (p1.TotalCount, p1.Items.Count, p2.Items.Count));                 // 5 completed rows spread over two pages
        Assert.All(p1.Items.Concat(p2.Items), r => Assert.Equal("Completed", r.ExecutionStatus));
        Assert.Equal(5, p1.Items.Concat(p2.Items).Select(x => x.EaTaskId).Distinct().Count());
    }

    [Theory]
    [InlineData(0, 0, 1, 50)]
    [InlineData(-4, -1, 1, 50)]
    [InlineData(2, 100000, 2, 200)]
    public async Task PageBounds_AreClamped_LikeTheTaskWorkspace(int page, int size, int expectedPage, int expectedSize)
    {
        var w = await NewWorldAsync(); await using var _ = w.Db;

        var r = await w.Svc.GetTasksAsync(new() { Page = page, PageSize = size }, default);

        Assert.Equal((expectedPage, expectedSize, 0), (r.PageNumber, r.PageSize, r.TotalCount));
    }

    // ---------------- reconciliation ----------------
    private static async Task AssertReconcilesAsync(World w, EmReportOverviewQueryDto cohort)
    {
        var q = new EmReportTaskRegisterQueryDto { FromDate = cohort.FromDate, ToDate = cohort.ToDate, BusinessModuleId = cohort.BusinessModuleId, PageSize = 200 };
        var rows = (await w.Svc.GetTasksAsync(q, default)).Items;
        var o = await w.Svc.GetOverviewAsync(cohort, default);
        var a = await w.Svc.GetAttentionAsync(cohort, default);
        var modules = await w.Svc.GetModulesAsync(cohort, default);

        Assert.Equal(o.TotalTasks, rows.Count);
        Assert.Equal(o.Paused, rows.Count(r => r.IsPaused));
        Assert.Equal(o.NotStarted, rows.Count(r => r.ExecutionStatus == "NotStarted"));
        Assert.Equal(o.InProgress, rows.Count(r => r.ExecutionStatus == "InProgress" && !r.IsPaused));
        Assert.Equal(o.Completed, rows.Count(r => r.ExecutionStatus == "Completed"));
        Assert.Equal(o.Cancelled, rows.Count(r => r.ExecutionStatus == "Cancelled"));
        Assert.Equal(a.Delayed, rows.Count(r => r.Performance == "Delayed"));
        Assert.Equal(o.DelayedTasks, rows.Count(r => r.Performance == "Delayed"));
        Assert.Equal(o.OnTimeCompleted, rows.Count(r => r.ExecutionStatus == "Completed" && r.Performance == "OnTime"));
        Assert.Equal(o.DelayedCompleted, rows.Count(r => r.ExecutionStatus == "Completed" && r.Performance == "Delayed"));
        Assert.Equal(o.NotMeasuredCompleted, rows.Count(r => r.ExecutionStatus == "Completed" && r.Performance == "NotMeasured"));
        Assert.Equal(a.TasksRequiringFollowup, rows.Count(r => r.FollowupStatus == "Pending"));
        Assert.Equal(a.OpenEscalations, rows.Sum(r => r.OpenEscalationCount));

        var byModule = rows.GroupBy(r => r.BusinessModuleId).ToDictionary(g => g.Key);
        Assert.Equal(modules.Select(m => m.BusinessModuleId).OrderBy(x => x), byModule.Keys.OrderBy(x => x));
        foreach (var m in modules)
        {
            var g = byModule[m.BusinessModuleId].ToList();
            Assert.Equal(m.TotalTasks, g.Count);
            Assert.Equal(m.Paused, g.Count(r => r.IsPaused));
            Assert.Equal(m.Delayed, g.Count(r => r.Performance == "Delayed"));
            Assert.Equal(m.TasksRequiringFollowup, g.Count(r => r.FollowupStatus == "Pending"));
            Assert.Equal(m.OpenEscalations, g.Sum(r => r.OpenEscalationCount));
        }
    }

    [Fact]
    public async Task Register_ReconcilesWithOverviewAttentionAndModules_Unfiltered()
    {
        var w = await RichAsync(); await using var _ = w.Db;
        await AssertReconcilesAsync(w, new());
    }

    [Fact]
    public async Task Register_Reconciles_WithADateFilter_AndEveryModuleFilter()
    {
        var w = await RichAsync(); await using var _ = w.Db;
        w.Db.Tasks.Add(T(w.M["Meeting"], "m-old", EaTaskExecutionStatus.InProgress, Base.AddDays(-20))); w.Db.Followups.Add(F(w.M["Meeting"], "m-old"));
        await w.Db.SaveChangesAsync();

        await AssertReconcilesAsync(w, new() { FromDate = new DateTime(2026, 9, 10), ToDate = new DateTime(2026, 9, 10) });
        foreach (var m in w.M.Values) await AssertReconcilesAsync(w, new() { BusinessModuleId = m.Id });
    }

    // ---------------- read-only ----------------
    [Fact]
    public async Task TaskRegister_WritesNothing_AndFreezesNoTat()
    {
        var w = await RichAsync(); await using var _ = w.Db;
        var before = (await w.Db.Tasks.CountAsync(), await w.Db.Followups.CountAsync(), await w.Db.Escalations.CountAsync(),
            await w.Db.AuditLogs.CountAsync(), await w.Db.Notifications.CountAsync());
        var frozen = await w.Db.Tasks.AsNoTracking().Where(t => t.ExecutionStatus == "InProgress").Select(t => t.TatUsedMinutes).ToListAsync();

        await Get(w);
        await Get(w, q => q.RequiresFollowup = true);

        Assert.False(w.Db.ChangeTracker.HasChanges());
        Assert.Equal(before, (await w.Db.Tasks.CountAsync(), await w.Db.Followups.CountAsync(), await w.Db.Escalations.CountAsync(),
            await w.Db.AuditLogs.CountAsync(), await w.Db.Notifications.CountAsync()));
        Assert.Equal(frozen, await w.Db.Tasks.AsNoTracking().Where(t => t.ExecutionStatus == "InProgress").Select(t => t.TatUsedMinutes).ToListAsync());
    }
}
