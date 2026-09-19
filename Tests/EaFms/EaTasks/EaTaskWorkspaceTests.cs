using System;
using System.Collections.Generic;
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

namespace Jarvis5.Tests.EaFms.EaTasks;

/// <summary>
/// Follow-up &amp; Escalation step 2: the central task workspace read API
/// (EaTaskService.QueryWorkspaceAsync). The workspace reads ea_tasks only — module
/// identity is data, never a hardcoded switch — and must never create Followup rows.
/// </summary>
public class EaTaskWorkspaceTests
{
    private static EaFmsDbContext MakeDb() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private static EaTaskService MakeService(EaFmsDbContext db) => new(
        db, new EaTaskRepository(db), new TatRuleRepository(db), new CreateEaTaskDtoValidator(),
        Mock.Of<ICurrentUserService>(u => u.UserId == 1), Mock.Of<IAuditService>());

    private static readonly DateTime Base = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>Module ids are deliberately not the 4/5/6/7 of the developer database.</summary>
    private static async Task<Dictionary<string, BusinessModule>> SeedModulesAsync(EaFmsDbContext db)
    {
        var modules = new[] { "Meeting", "Travel & Hospitality", "EA Approval", "Delegation" }
            .Select(n => new BusinessModule { Name = n, IsActive = true, CreatedBy = "seed", CreatedDate = Base }).ToList();
        db.BusinessModules.AddRange(modules);
        await db.SaveChangesAsync();
        return modules.ToDictionary(m => m.Name);
    }

    private static EaTask AddTask(EaFmsDbContext db, BusinessModule m, string recordId, string title,
        string status = EaTaskExecutionStatus.NotStarted, int minutesOffset = 0, string? description = null,
        bool deleted = false, bool active = true, DateTime? created = null) =>
        db.Tasks.Add(new EaTask
        {
            BusinessModuleId = m.Id, ModuleName = m.Name, BusinessRecordId = recordId, Task = title,
            Description = description, ExecutionStatus = status, IsActive = active, IsDeleted = deleted,
            CreatedBy = "1", CreatedDate = created ?? Base.AddMinutes(minutesOffset)
        }).Entity;

    private static async Task<(EaFmsDbContext Db, Dictionary<string, BusinessModule> Modules)> SeededAsync()
    {
        var db = MakeDb();
        var m = await SeedModulesAsync(db);
        AddTask(db, m["Meeting"], "11", "Quarterly board meeting", EaTaskExecutionStatus.InProgress, 1, "Board review");
        AddTask(db, m["Travel & Hospitality"], "21", "TRV-2026-000021", EaTaskExecutionStatus.NotStarted, 2, "Delhi trip");
        AddTask(db, m["EA Approval"], "31", "APR-2026-000031", EaTaskExecutionStatus.InProgress, 3);
        AddTask(db, m["Delegation"], "41", "Delegate vendor follow-up", EaTaskExecutionStatus.Completed, 4);
        await db.SaveChangesAsync();
        return (db, m);
    }

    private static EaTaskWorkspaceQueryDto Q(Action<EaTaskWorkspaceQueryDto>? tweak = null)
    {
        var q = new EaTaskWorkspaceQueryDto();
        tweak?.Invoke(q);
        return q;
    }

    // 1 + 8-11: no module filter returns every module (Meeting, Travel, Approval, Delegation).
    [Fact]
    public async Task NoFilter_ReturnsTasksFromAllModules()
    {
        var (db, m) = await SeededAsync();
        await using var _ = db;

        var result = await MakeService(db).QueryWorkspaceAsync(Q(), default);

        Assert.Equal(4, result.TotalCount);
        Assert.Equal(new HashSet<string> { "Meeting", "Travel & Hospitality", "EA Approval", "Delegation" },
            result.Items.Select(i => i.ModuleName).ToHashSet());
        Assert.Contains(result.Items, i => i.ModuleId == m["Meeting"].Id && i.BusinessRecordId == "11");
        Assert.Contains(result.Items, i => i.ModuleId == m["Travel & Hospitality"].Id && i.BusinessRecordId == "21");
        Assert.Contains(result.Items, i => i.ModuleId == m["EA Approval"].Id && i.BusinessRecordId == "31");
        Assert.Contains(result.Items, i => i.ModuleId == m["Delegation"].Id && i.BusinessRecordId == "41");
    }

    // A module that did not exist when this was written appears with no code change.
    [Fact]
    public async Task FutureModuleTask_AppearsAutomatically()
    {
        var (db, _) = await SeededAsync();
        await using var _ = db;
        var future = new BusinessModule { Name = "Vendor Management", IsActive = true, CreatedBy = "seed", CreatedDate = Base };
        db.BusinessModules.Add(future);
        await db.SaveChangesAsync();
        AddTask(db, future, "99", "Onboard vendor", minutesOffset: 10);
        await db.SaveChangesAsync();

        var result = await MakeService(db).QueryWorkspaceAsync(Q(), default);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal("Vendor Management", result.Items[0].ModuleName);
    }

    // 2
    [Fact]
    public async Task BusinessModuleIdFilter_ReturnsOnlyThatModule()
    {
        var (db, m) = await SeededAsync();
        await using var _ = db;
        AddTask(db, m["Meeting"], "12", "Second meeting", minutesOffset: 5);
        await db.SaveChangesAsync();

        var result = await MakeService(db).QueryWorkspaceAsync(Q(q => q.BusinessModuleId = m["Meeting"].Id), default);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, i => Assert.Equal(m["Meeting"].Id, i.ModuleId));
    }

    [Fact]
    public async Task BusinessRecordIdFilter_ReturnsOnlyThatRecord()
    {
        var (db, m) = await SeededAsync();
        await using var _ = db;

        var result = await MakeService(db).QueryWorkspaceAsync(
            Q(q => { q.BusinessModuleId = m["Travel & Hospitality"].Id; q.BusinessRecordId = " 21 "; }), default);

        Assert.Equal("21", Assert.Single(result.Items).BusinessRecordId);
    }

    // 3
    [Theory]
    [InlineData("InProgress", 2)]
    [InlineData("inprogress", 2)]
    [InlineData("NotStarted", 1)]
    [InlineData("Completed", 1)]
    [InlineData("Cancelled", 0)]
    public async Task ExecutionStatusFilter_MatchesCentralStatus(string status, int expected)
    {
        var (db, _) = await SeededAsync();
        await using var __ = db;

        var result = await MakeService(db).QueryWorkspaceAsync(Q(q => q.ExecutionStatus = status), default);

        Assert.Equal(expected, result.TotalCount);
        Assert.All(result.Items, i => Assert.Equal(EaTaskExecutionStatus.Canonicalize(status), i.ExecutionStatus));
    }

    [Theory]
    [InlineData("Resumed")]
    [InlineData("Paused")]
    [InlineData("nonsense")]
    public async Task ExecutionStatusFilter_UnknownValue_IsRejected(string status)
    {
        var (db, _) = await SeededAsync();
        await using var __ = db;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            MakeService(db).QueryWorkspaceAsync(Q(q => q.ExecutionStatus = status), default));
    }

    // 4
    [Theory]
    [InlineData("board", 1)]        // Task title
    [InlineData("DELHI", 1)]        // Description, case-insensitive
    [InlineData("delegation", 1)]   // ModuleName
    [InlineData("APR-2026", 1)]     // title of the approval task
    [InlineData("41", 1)]           // BusinessRecordId
    [InlineData("zzz-no-match", 0)]
    public async Task Search_CoversTitleDescriptionModuleAndRecord(string term, int expected)
    {
        var (db, _) = await SeededAsync();
        await using var __ = db;

        var result = await MakeService(db).QueryWorkspaceAsync(Q(q => q.Search = term), default);

        Assert.Equal(expected, result.TotalCount);
        Assert.Equal(expected, result.Items.Count);
    }

    [Fact]
    public async Task EmptyResult_ReturnsEmptyPageNot404()
    {
        var (db, _) = await SeededAsync();
        await using var __ = db;

        var result = await MakeService(db).QueryWorkspaceAsync(Q(q => q.Search = "nothing"), default);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        Assert.Equal(1, result.PageNumber);
    }

    // 5
    [Fact]
    public async Task Pagination_SlicesPagesAndReportsMetadata()
    {
        await using var db = MakeDb();
        var m = await SeedModulesAsync(db);
        for (var i = 1; i <= 25; i++) AddTask(db, m["Meeting"], i.ToString(), $"Task {i}", minutesOffset: i);
        await db.SaveChangesAsync();
        var svc = MakeService(db);

        var p1 = await svc.QueryWorkspaceAsync(Q(q => { q.Page = 1; q.PageSize = 10; }), default);
        var p3 = await svc.QueryWorkspaceAsync(Q(q => { q.Page = 3; q.PageSize = 10; }), default);
        var p4 = await svc.QueryWorkspaceAsync(Q(q => { q.Page = 4; q.PageSize = 10; }), default);

        Assert.Equal(10, p1.Items.Count);
        Assert.Equal(5, p3.Items.Count);
        Assert.Empty(p4.Items);
        Assert.Equal(25, p1.TotalCount);
        Assert.Equal(3, p1.TotalPages);
        Assert.Equal((1, 10), (p1.PageNumber, p1.PageSize));
        Assert.Equal(3, p3.PageNumber);
        Assert.Equal(25, p4.TotalCount);
    }

    [Theory]
    [InlineData(0, 0, 1, 50)]
    [InlineData(-3, -1, 1, 50)]
    [InlineData(2, 10000, 2, 200)]
    public async Task Pagination_BoundsAreClamped(int page, int pageSize, int expectedPage, int expectedSize)
    {
        await using var db = MakeDb();

        var result = await MakeService(db).QueryWorkspaceAsync(Q(q => { q.Page = page; q.PageSize = pageSize; }), default);

        Assert.Equal((expectedPage, expectedSize), (result.PageNumber, result.PageSize));
    }

    // 6
    [Fact]
    public async Task Ordering_NewestCreatedFirst_IdBreaksTies_AndPagesDoNotOverlap()
    {
        await using var db = MakeDb();
        var m = await SeedModulesAsync(db);
        var same = Base.AddHours(1);
        var older = AddTask(db, m["Meeting"], "1", "older", created: Base);
        var tieA = AddTask(db, m["Meeting"], "2", "tieA", created: same);
        var tieB = AddTask(db, m["Delegation"], "3", "tieB", created: same);
        var newest = AddTask(db, m["Travel & Hospitality"], "4", "newest", created: Base.AddHours(2));
        await db.SaveChangesAsync();
        var svc = MakeService(db);

        var all = await svc.QueryWorkspaceAsync(Q(), default);
        var page1 = await svc.QueryWorkspaceAsync(Q(q => { q.Page = 1; q.PageSize = 2; }), default);
        var page2 = await svc.QueryWorkspaceAsync(Q(q => { q.Page = 2; q.PageSize = 2; }), default);

        Assert.Equal(new[] { newest.Id, tieB.Id, tieA.Id, older.Id }, all.Items.Select(i => i.EaTaskId));
        Assert.Equal(all.Items.Select(i => i.EaTaskId),
            page1.Items.Concat(page2.Items).Select(i => i.EaTaskId));
    }

    // 7: existing central-task visibility rule = not deleted; IsActive is exposed, not filtered.
    [Fact]
    public async Task Visibility_DeletedExcluded_InactiveIncludedAndFlagged()
    {
        await using var db = MakeDb();
        var m = await SeedModulesAsync(db);
        AddTask(db, m["Meeting"], "1", "live");
        AddTask(db, m["Meeting"], "2", "deleted", deleted: true);
        AddTask(db, m["Meeting"], "3", "inactive completed", EaTaskExecutionStatus.Completed, active: false);
        await db.SaveChangesAsync();

        var result = await MakeService(db).QueryWorkspaceAsync(Q(), default);

        Assert.Equal(2, result.TotalCount);
        Assert.DoesNotContain(result.Items, i => i.Task == "deleted");
        Assert.False(result.Items.Single(i => i.Task == "inactive completed").IsActive);
        Assert.True(result.Items.Single(i => i.Task == "live").IsActive);
    }

    // Row contract: identity + existing derived TAT/pause values are reused from the central mapper.
    [Fact]
    public async Task Rows_ExposeIdentityStatusAndExistingTatAndPauseValues()
    {
        await using var db = MakeDb();
        var m = await SeedModulesAsync(db);
        var wf = new WorkflowInstance { BusinessModuleId = m["Meeting"].Id, BusinessRecordId = "5", CreatedBy = "seed", CreatedDate = Base };
        db.WorkflowInstances.Add(wf);
        await db.SaveChangesAsync();
        var started = DateTime.UtcNow.AddMinutes(-90);
        db.WorkPauses.Add(new WorkPause { WorkflowInstanceId = wf.Id, StartAt = DateTime.UtcNow.AddMinutes(-10), CreatedBy = "seed", CreatedDate = Base });
        var task = AddTask(db, m["Meeting"], "5", "Paused meeting", EaTaskExecutionStatus.InProgress, description: "d");
        task.WorkflowInstanceId = wf.Id;
        task.Type = "Internal"; task.Subtype = "Review";
        task.AllottedTatMinutes = 120; task.StartedAt = started;
        await db.SaveChangesAsync();

        var row = Assert.Single((await MakeService(db).QueryWorkspaceAsync(Q(), default)).Items);

        Assert.Equal(task.Id, row.EaTaskId);
        Assert.Equal(m["Meeting"].Id, row.ModuleId);
        Assert.Equal("5", row.BusinessRecordId);
        Assert.Equal("d", row.Description);
        Assert.Equal(("Internal", "Review"), (row.Type, row.Subtype));
        // Pause is derived, never a persisted status: ExecutionStatus stays InProgress.
        Assert.Equal("InProgress", row.ExecutionStatus);
        Assert.True(row.IsPaused);
        Assert.Equal(1, row.PauseCount);
        Assert.Equal(120, row.AllottedTatMinutes);
        Assert.NotNull(row.CurrentTatUsedMinutes);
        Assert.Equal(120 - row.CurrentTatUsedMinutes, row.CurrentTatDifferenceMinutes);
        Assert.Equal("1", row.CreatedBy);
    }

    [Fact]
    public async Task PersistedExecutionStatusVocabulary_HasNoPausedOrResumed()
    {
        Assert.Equal(new[] { "Cancelled", "Completed", "InProgress", "NotStarted" },
            EaTaskExecutionStatus.Values.OrderBy(v => v, StringComparer.Ordinal));
        await Task.CompletedTask;
    }

    // 12
    [Fact]
    public async Task Workspace_DoesNotCreateFollowupRows()
    {
        var (db, _) = await SeededAsync();
        await using var __ = db;
        var svc = MakeService(db);

        await svc.QueryWorkspaceAsync(Q(), default);
        await svc.QueryWorkspaceAsync(Q(q => q.Search = "board"), default);

        Assert.Equal(0, await db.Followups.CountAsync());
        Assert.Equal(0, await db.Escalations.CountAsync());
        Assert.Equal(4, await db.Tasks.CountAsync());
    }

    // 13 + 14: the existing single-task and history endpoints are untouched.
    [Fact]
    public async Task ExistingGetAsync_AndQueryAsync_StillWork()
    {
        var (db, m) = await SeededAsync();
        await using var __ = db;
        var svc = MakeService(db);
        var id = (await db.Tasks.FirstAsync(t => t.BusinessRecordId == "21")).Id;

        var one = await svc.GetAsync(id, default);
        var legacyList = await svc.QueryAsync(m["Meeting"].Id, "11", default);

        Assert.Equal("TRV-2026-000021", one.Task);
        Assert.Equal("Travel & Hospitality", one.ModuleName);
        Assert.Single(legacyList);
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetAsync(999999, default));
    }

    [Fact]
    public async Task ExistingGetHistoryAsync_StillReturnsCreatedEvent()
    {
        var (db, _) = await SeededAsync();
        await using var __ = db;
        var id = (await db.Tasks.FirstAsync(t => t.BusinessRecordId == "21")).Id;

        var history = await MakeService(db).GetHistoryAsync(id, default);

        Assert.Contains(history, e => e.EventType == "Created");
    }
}
