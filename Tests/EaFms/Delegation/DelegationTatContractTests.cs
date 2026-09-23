using System;
using System.IO;
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
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using UglyToad.PdfPig.Writer;
using Xunit;
using DelegationEntity = Jarvis5.Entities.EaFms.Delegation;

namespace Jarvis5.Tests.EaFms.Delegation;

/// <summary>
/// The Delegation API must show the whole TAT lifecycle (allotted, live, frozen, started, paused, completed) using values that
/// live only on the central EaTask / WorkPause rows — and must reconcile with the EaTask API and the EM Report task register.
/// Runs the real services against a throwaway PostgreSQL database (see ScratchTatDatabase).
/// </summary>
public class DelegationTatContractTests : IClassFixture<ScratchTatDatabase>, IDisposable
{
    private readonly ScratchTatDatabase _fx;
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private static readonly ICurrentUserService User = Mock.Of<ICurrentUserService>(u => u.UserName == "tat-actor" && u.UserId == 7L);

    public DelegationTatContractTests(ScratchTatDatabase fx)
    {
        _fx = fx;
        Directory.CreateDirectory(_root);
    }

    public void Dispose() { try { Directory.Delete(_root, true); } catch { /* best-effort */ } }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private EaTaskService NewEaTaskService(EaFmsDbContext db) =>
        new(db, new EaTaskRepository(db), new TatRuleRepository(db), new CreateEaTaskDtoValidator(), User, new AuditService(db, User));

    private DelegationService NewService(EaFmsDbContext db) =>
        new(db, User, new AuditService(db, User), new DelegationRepository(db), NewEaTaskService(db),
            Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(e => e.ContentRootPath == _root),
            new TaskReviewService(db, new TaskReviewRepository(db), User, new AuditService(db, User)), new TatRuleRepository(db));

    private async Task<DelegationResponseDto> Run(Func<DelegationService, Task<DelegationResponseDto>> action)
    {
        await using var db = _fx.Db();
        return await action(NewService(db));
    }

    private async Task<string> RuleAsync(int minutes)
    {
        var type = Unique("Contract Type");
        await using var db = _fx.Db();
        db.TatRules.Add(new TatRule { BusinessModuleId = _fx.DelegationModuleId, ModuleName = "Delegation", Type = type, Subtype = null, TaskType = DelegationTaskType.Actual,
            TatMinutes = minutes, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return type;
    }

    private Task<DelegationResponseDto> CreateAsync(string? type, DateTime? startDate = null, DateTime? endDate = null) =>
        Run(s => s.CreateAsync(new DelegationCreateRequestDto
        {
            Title = "Contract " + Guid.NewGuid().ToString("N")[..8], DoerId = "EMP-C", DelegationType = type, StartDate = startDate, EndDate = endDate
        }));

    private static IFormFile Pdf()
    {
        var b = new PdfDocumentBuilder();
        b.AddPage(200, 200);
        var ms = new MemoryStream(b.Build());
        return new FormFile(ms, 0, ms.Length, "completionPdf", "done.pdf") { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
    }

    private async Task BackdateStartAsync(DelegationResponseDto d, int minutesAgo)
    {
        await using var db = _fx.Db();
        var at = DateTime.UtcNow.AddMinutes(-minutesAgo);
        (await db.Delegations.SingleAsync(x => x.Id == d.DelegationId)).StartedAt = at;
        (await db.Tasks.SingleAsync(t => t.Id == d.EaTaskId)).StartedAt = at;
        await db.SaveChangesAsync();
    }

    private async Task SetPauseWindowAsync(DelegationResponseDto d, int startOffset, int? endOffset)
    {
        await using var db = _fx.Db();
        var task = await db.Tasks.SingleAsync(t => t.Id == d.EaTaskId);
        var pause = await db.WorkPauses.SingleAsync(p => p.WorkflowInstanceId == task.WorkflowInstanceId);
        pause.StartAt = task.StartedAt!.Value.AddMinutes(startOffset);
        pause.EndAt = endOffset.HasValue ? task.StartedAt.Value.AddMinutes(endOffset.Value) : null;
        await db.SaveChangesAsync();
    }

    private Task<DelegationResponseDto> Get(DelegationResponseDto d) => Run(s => s.GetByIdAsync(d.DelegationId));

    private async Task<EaTaskResponseDto> EaTaskView(DelegationResponseDto d)
    {
        await using var db = _fx.Db();
        return await NewEaTaskService(db).GetAsync(d.EaTaskId, default);
    }

    private async Task<EmReportTaskRowDto> EmRow(DelegationResponseDto d)
    {
        await using var db = _fx.Db();
        var page = await new EmReportService(db).GetTasksAsync(new EmReportTaskRegisterQueryDto { Search = d.Title, PageSize = 50 }, default);
        return Assert.Single(page.Items);
    }

    /// <summary>The same TAT facts through the Delegation API, the EaTask API and the EM Report task register.</summary>
    private async Task AssertReconciledAsync(DelegationResponseDto viaDelegation)
    {
        var task = await EaTaskView(viaDelegation);
        var em = await EmRow(viaDelegation);

        Assert.Equal(task.AllottedTatMinutes, viaDelegation.AllottedTatMinutes);
        Assert.Equal(task.AllottedTatMinutes, em.AllottedTatMinutes);
        // viaDelegation.TatUsedMinutes is now the strict Meeting-aligned LIVE public value (matches the
        // EaTask API's own live CurrentTatUsedMinutes and EM Report's CurrentOrFinalTatUsedMinutes) —
        // not the EaTask's separate internal frozen TatUsedMinutes column, which stays untouched and is
        // no longer compared here since it is not part of Delegation's public contract.
        Assert.Equal(task.CurrentTatUsedMinutes, viaDelegation.TatUsedMinutes);
        Assert.Equal(task.CurrentTatUsedMinutes, em.CurrentOrFinalTatUsedMinutes);
        Assert.Equal(task.ExecutionStatus, viaDelegation.ExecutionStatus);
        Assert.Equal(task.ExecutionStatus, em.ExecutionStatus);
        Assert.Equal(task.IsPaused ?? false, viaDelegation.IsPaused);
        Assert.Equal(task.IsPaused ?? false, em.IsPaused);
        Assert.Equal(task.StartedAt, viaDelegation.StartedAt);
        Assert.Equal(task.CompletedAt, viaDelegation.CompletedAt);
    }

    // ---------------- create ----------------
    [Fact]
    public async Task Create_WithAConfiguredType_ShowsTheAllottedTat_AndAnUnstartedState()
    {
        var type = await RuleAsync(240);

        var created = await CreateAsync(type);

        Assert.Equal(240, created.AllottedTatMinutes);
        Assert.Equal(EaTaskExecutionStatus.NotStarted, created.ExecutionStatus);
        Assert.False(created.IsPaused);
        Assert.Null(created.StartedAt);
        Assert.Null(created.CompletedAt);
        Assert.Null(created.TatUsedMinutes);   // null, not 0, before the clock starts (same as Meeting)
        await using var db = _fx.Db();
        var task = await db.Tasks.AsNoTracking().SingleAsync(t => t.Id == created.EaTaskId);
        Assert.NotNull(task.TatRuleId);                // the snapshot lives on ea_tasks
        Assert.Equal(240, task.AllottedTatMinutes);
        await AssertReconciledAsync(created);
    }

    // ---------------- start ----------------
    [Fact]
    public async Task Start_SetsStartedAtAndInProgress_TheClockRunsFromStartedAt_NotFromTheStartDate()
    {
        var type = await RuleAsync(240);
        var plannedStart = DateTime.UtcNow.AddDays(-5);   // planned five days ago: irrelevant to TAT
        var created = await CreateAsync(type, startDate: plannedStart);

        var started = await Run(s => s.StartAsync(created.DelegationId));

        Assert.NotNull(started.StartedAt);
        Assert.Equal(EaTaskExecutionStatus.InProgress, started.ExecutionStatus);
        Assert.False(started.IsPaused);
        Assert.True(Math.Abs((plannedStart - started.StartDate!.Value).TotalMilliseconds) < 1);   // startDate untouched
        Assert.NotEqual(started.StartDate, started.StartedAt);
        Assert.InRange(started.TatUsedMinutes!.Value, 0, 1);                                      // not 5 days
        await AssertReconciledAsync(started);
    }

    [Fact]
    public async Task Running_CurrentTatIsCountedFromStartedAt_AndKeepsIncreasing()
    {
        var type = await RuleAsync(240);
        var created = await CreateAsync(type);
        await Run(s => s.StartAsync(created.DelegationId));

        await BackdateStartAsync(created, 75);
        var at75 = await Get(created);
        await BackdateStartAsync(created, 90);
        var at90 = await Get(created);

        Assert.Equal(75, at75.TatUsedMinutes);
        Assert.Equal(90, at90.TatUsedMinutes);
        Assert.Equal(240, at90.AllottedTatMinutes);
        await AssertReconciledAsync(at90);
        var list = (await Run2(s => s.ListAsync(new DelegationListQueryDto { DoerId = "EMP-C", PageSize = 200 }))).Items.Single(i => i.DelegationId == created.DelegationId);
        Assert.Equal(90, list.TatUsedMinutes);
    }

    private async Task<T> Run2<T>(Func<DelegationService, Task<T>> action)
    {
        await using var db = _fx.Db();
        return await action(NewService(db));
    }

    // ---------------- pause / resume ----------------
    [Fact]
    public async Task Pause_ShowsPausedWhileStaying_InProgress_AndTheClockStopsIncreasing()
    {
        var type = await RuleAsync(240);
        var created = await CreateAsync(type);
        await Run(s => s.StartAsync(created.DelegationId));
        await BackdateStartAsync(created, 90);   // started 90 minutes ago

        var paused = await Run(s => s.PauseAsync(created.DelegationId, null));
        await SetPauseWindowAsync(created, 30, null);   // paused since minute 30: only 30 active minutes so far

        var view = await Get(created);
        Assert.True(paused.IsPaused);
        Assert.Equal(EaTaskExecutionStatus.InProgress, paused.ExecutionStatus);   // Paused is never a persisted status
        Assert.True(view.IsPaused);
        Assert.Equal(EaTaskExecutionStatus.InProgress, view.ExecutionStatus);
        Assert.Equal(30, view.TatUsedMinutes);   // not 90
        Assert.Equal(30, (await Get(created)).TatUsedMinutes);   // and not moving while paused
        await AssertReconciledAsync(view);
    }

    [Fact]
    public async Task Resume_ContinuesFromThePreviousActiveDuration_WithoutCountingThePause()
    {
        var type = await RuleAsync(240);
        var created = await CreateAsync(type);
        await Run(s => s.StartAsync(created.DelegationId));
        await BackdateStartAsync(created, 180);
        await Run(s => s.PauseAsync(created.DelegationId, null));

        var resumed = await Run(s => s.ResumeAsync(created.DelegationId));
        await SetPauseWindowAsync(created, 30, 90);   // a 60-minute pause inside 180 elapsed minutes

        var view = await Get(created);
        Assert.False(resumed.IsPaused);
        Assert.Equal(EaTaskExecutionStatus.InProgress, resumed.ExecutionStatus);
        Assert.False(view.IsPaused);
        Assert.Equal(120, view.TatUsedMinutes);   // 180 - 60; not restarted from zero and not 180
        await AssertReconciledAsync(view);
    }

    // ---------------- complete ----------------
    [Fact]
    public async Task Complete_FreezesTheFinalActiveTat_ReturnsItInTheResponse_AndItNeverIncreasesAgain()
    {
        var type = await RuleAsync(240);
        var created = await CreateAsync(type);
        await Run(s => s.StartAsync(created.DelegationId));
        await BackdateStartAsync(created, 180);
        await Run(s => s.PauseAsync(created.DelegationId, null));
        await Run(s => s.ResumeAsync(created.DelegationId));
        await SetPauseWindowAsync(created, 30, 90);

        var completed = await Run(s => s.CompleteAndApproveAsync(created.DelegationId, Pdf()));

        Assert.Equal(DelegationStatus.Completed, completed.Status);
        Assert.Equal(EaTaskExecutionStatus.Completed, completed.ExecutionStatus);
        Assert.NotNull(completed.CompletedAt);
        Assert.False(completed.IsPaused);
        Assert.Equal(120, completed.TatUsedMinutes);   // strict Meeting-aligned public value, stable once Completed
        Assert.Equal(240, completed.AllottedTatMinutes);
        Assert.NotNull(completed.CompletionPdfAttachmentId);  // completionPdf handling untouched
        await using var db = _fx.Db();
        var task = await db.Tasks.AsNoTracking().SingleAsync(t => t.Id == created.EaTaskId);
        // The EaTask's own internal frozen column is a separate, untouched concern (EaTask API / EM Report) —
        // no longer part of Delegation's public contract, but still correct internally.
        Assert.Equal(120, task.TatUsedMinutes);
        Assert.Equal(120, EaTaskService.CalculateCurrentTatUsedMinutes(task, Array.Empty<WorkPause>(), DateTime.UtcNow.AddDays(3)));   // far later: unchanged
        var later = await Get(created);
        Assert.Equal(120, later.TatUsedMinutes);
        await AssertReconciledAsync(later);
    }

    // ---------------- no TAT / dates ----------------
    [Fact]
    public async Task NoTatDelegation_ReturnsNullTatValuesAtEveryStep_NeverZero()
    {
        var created = await CreateAsync(null);
        Assert.Equal((null, null), (created.AllottedTatMinutes, created.TatUsedMinutes));
        Assert.Equal(EaTaskExecutionStatus.NotStarted, created.ExecutionStatus);

        var started = await Run(s => s.StartAsync(created.DelegationId));
        await BackdateStartAsync(created, 50);
        var running = await Get(created);
        var paused = await Run(s => s.PauseAsync(created.DelegationId, null));
        var resumed = await Run(s => s.ResumeAsync(created.DelegationId));
        var completed = await Run(s => s.CompleteAndApproveAsync(created.DelegationId, null));

        foreach (var step in new[] { started, running, paused, resumed, completed })
            Assert.Equal((null, null), (step.AllottedTatMinutes, step.TatUsedMinutes));
        Assert.Equal(EaTaskExecutionStatus.InProgress, running.ExecutionStatus);
        Assert.True(paused.IsPaused);
        Assert.Equal(EaTaskExecutionStatus.Completed, completed.ExecutionStatus);
        Assert.NotNull(completed.CompletedAt);
        await AssertReconciledAsync(completed);
    }

    [Fact]
    public async Task EndDate_DoesNotReplaceTat_ForATatEnabledDelegation()
    {
        var type = await RuleAsync(240);
        var created = await CreateAsync(type, endDate: IndiaBusinessCalendar.Today.AddDays(-30));   // long past
        await Run(s => s.StartAsync(created.DelegationId));
        await BackdateStartAsync(created, 60);
        var completed = await Run(s => s.CompleteAndApproveAsync(created.DelegationId, null));

        Assert.True(completed.EndDate < DateTime.UtcNow);
        Assert.Equal(60, completed.TatUsedMinutes);                 // measured by TAT ...
        Assert.Equal("OnTime", (await EmRow(completed)).Performance);   // ... so within 240 minutes is OnTime despite the past end date
        Assert.Equal(created.EndDate, (await EmRow(completed)).DueDate);   // the end date is still shown, not evaluated
    }

    // ---------------- list ----------------
    [Fact]
    public async Task List_ShowsTheTatLifecycleOfEveryRow_InOneBatch()
    {
        var type = await RuleAsync(100);
        var idle = await CreateAsync(type);
        var running = await CreateAsync(type);
        await Run(s => s.StartAsync(running.DelegationId));
        await BackdateStartAsync(running, 40);
        var paused = await CreateAsync(type);
        await Run(s => s.StartAsync(paused.DelegationId));
        await BackdateStartAsync(paused, 40);
        await Run(s => s.PauseAsync(paused.DelegationId, null));
        await SetPauseWindowAsync(paused, 10, null);
        var noTat = await CreateAsync(null);

        var items = (await Run2(s => s.ListAsync(new DelegationListQueryDto { DoerId = "EMP-C", PageSize = 200 }))).Items;
        DelegationResponseDto Row(DelegationResponseDto d) => items.Single(i => i.DelegationId == d.DelegationId);

        Assert.Equal((EaTaskExecutionStatus.NotStarted, false, (int?)100, (int?)null), (Row(idle).ExecutionStatus, Row(idle).IsPaused, Row(idle).AllottedTatMinutes, Row(idle).TatUsedMinutes));
        Assert.Equal((EaTaskExecutionStatus.InProgress, false, (int?)40), (Row(running).ExecutionStatus, Row(running).IsPaused, Row(running).TatUsedMinutes));
        Assert.Equal((EaTaskExecutionStatus.InProgress, true, (int?)10), (Row(paused).ExecutionStatus, Row(paused).IsPaused, Row(paused).TatUsedMinutes));
        Assert.Equal(((int?)null, (int?)null), (Row(noTat).AllottedTatMinutes, Row(noTat).TatUsedMinutes));
    }

    // ---------------- storage / contract ----------------
    [Fact]
    public void NoTatOrExecutionColumnsWereDuplicatedOntoDelegation_AndTheResponseUsesTheCanonicalEaTaskNames()
    {
        var entity = typeof(DelegationEntity).GetProperties().Select(p => p.Name).ToArray();
        foreach (var forbidden in new[] { "TatRuleId", "AllottedTatMinutes", "TatUsedMinutes", "CurrentTatUsedMinutes", "ExecutionStatus", "IsPaused", "Type", "Subtype" })
            Assert.DoesNotContain(forbidden, entity);

        using var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Assert.DoesNotContain(db.Model.FindEntityType(typeof(DelegationEntity))!.GetProperties().Select(p => p.GetColumnName()),
            c => c.Contains("Tat") || c == "ExecutionStatus");

        var response = typeof(DelegationResponseDto).GetProperties().Select(p => p.Name).ToArray();
        foreach (var n in new[] { "EaTaskId", "DelegationType", "StartDate", "EndDate", "StartedAt", "CompletedAt", "ExecutionStatus", "IsPaused", "AllottedTatMinutes", "TatUsedMinutes" })
            Assert.Contains(n, response);
        // the same names the central EaTask API already uses
        var eaTask = typeof(EaTaskResponseDto).GetProperties().Select(p => p.Name).ToArray();
        foreach (var n in new[] { "ExecutionStatus", "AllottedTatMinutes", "CurrentTatUsedMinutes", "TatUsedMinutes", "StartedAt", "CompletedAt" })
            Assert.Contains(n, eaTask);

        // Meeting-style TAT alignment: same property names/types as MeetingTatSummaryDto,
        // reused verbatim (not a re-declared shape) so the two never drift apart.
        foreach (var n in new[] { "TatPausedMinutes", "TatSummary" })
            Assert.Contains(n, response);
        Assert.Same(typeof(MeetingTatSummaryDto), typeof(DelegationResponseDto).GetProperty("TatSummary")!.PropertyType);
        var summaryProps = typeof(MeetingTatSummaryDto).GetProperties().Select(p => p.Name).ToArray();
        foreach (var n in new[] { "Tat", "TotalTat", "TatDifference", "StartTime", "EndTime", "LastActiveTime", "PauseTime", "PauseCount" })
            Assert.Contains(n, summaryProps);
    }

    // ================================================================
    // Strict Meeting TAT contract: Delegation's PUBLIC response must expose exactly
    // Meeting's own TAT columns — no Delegation-only "currentTatUsedMinutes" invention.
    // ================================================================

    [Fact]
    public void CurrentTatUsedMinutes_DoesNotExistOnDelegationPublicResponse_ItWasADelegationOnlyInventionMeetingNeverHad()
    {
        var response = typeof(DelegationResponseDto).GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("CurrentTatUsedMinutes", response);

        // The EaTask API keeps its own separate live/frozen split (CurrentTatUsedMinutes + TatUsedMinutes) —
        // that internal contract is untouched; it simply is not surfaced on the Delegation response any more.
        var eaTask = typeof(EaTaskResponseDto).GetProperties().Select(p => p.Name).ToArray();
        Assert.Contains("CurrentTatUsedMinutes", eaTask);
        Assert.Contains("TatUsedMinutes", eaTask);
    }

    [Fact]
    public void DelegationPublicTatFields_MatchMeetingsOwnTatFields_NameForNameAndTypeForType()
    {
        // Meeting's list DTO is the contract being mirrored: it is the one Meeting response type that
        // carries flat tatUsedMinutes/tatPausedMinutes alongside tatSummary (Meeting's detail DTO carries
        // only tatSummary — Meeting itself has no flat fields there). Delegation now matches the list shape
        // exactly, on every endpoint (Delegation has one response DTO, not a separate list/detail split).
        var meetingList = typeof(MeetingListItemResponseDto).GetProperties().Select(p => p.Name).ToArray();
        var delegationResponse = typeof(DelegationResponseDto).GetProperties().Select(p => p.Name).ToArray();

        foreach (var n in new[] { "TatUsedMinutes", "TatPausedMinutes", "TatSummary" })
        {
            Assert.Contains(n, meetingList);
            Assert.Contains(n, delegationResponse);
        }
        Assert.DoesNotContain("CurrentTatUsedMinutes", meetingList);
        Assert.DoesNotContain("CurrentTatUsedMinutes", delegationResponse);

        Assert.Equal(typeof(int?), typeof(MeetingListItemResponseDto).GetProperty("TatUsedMinutes")!.PropertyType);
        Assert.Equal(typeof(int?), typeof(DelegationResponseDto).GetProperty("TatUsedMinutes")!.PropertyType);
        Assert.Equal(typeof(int?), typeof(MeetingListItemResponseDto).GetProperty("TatPausedMinutes")!.PropertyType);
        Assert.Equal(typeof(int?), typeof(DelegationResponseDto).GetProperty("TatPausedMinutes")!.PropertyType);

        // Both point at the exact same MeetingTatSummaryDto type — never two independently-declared shapes.
        Assert.Same(typeof(MeetingTatSummaryDto), typeof(MeetingListItemResponseDto).GetProperty("TatSummary")!.PropertyType);
        Assert.Same(typeof(MeetingTatSummaryDto), typeof(DelegationResponseDto).GetProperty("TatSummary")!.PropertyType);
    }

    [Fact]
    public async Task Update_ReturnsTheSameStrictMeetingAlignedTatContract_UnaffectedByBusinessFieldEdits()
    {
        var type = await RuleAsync(240);
        var created = await CreateAsync(type);
        await Run(s => s.StartAsync(created.DelegationId));
        await BackdateStartAsync(created, 45);

        // Title left unchanged: EaTask.Task (what EM Report searches) is never re-synced from a Delegation
        // title edit (a separate, pre-existing, intentional behavior) — changing it here would make
        // AssertReconciledAsync's EM Report lookup-by-title miss, which is not what this test is checking.
        var updated = await Run(s => s.UpdateAsync(created.DelegationId, new DelegationUpdateRequestDto
        {
            Title = created.Title, DoerId = "EMP-C", Priority = "High"
        }));

        Assert.Equal(45, updated.TatUsedMinutes);
        Assert.Equal(0, updated.TatPausedMinutes);
        Assert.Equal(45, (int)updated.TatSummary.Tat!.Value.TotalMinutes);
        Assert.Equal(240, updated.AllottedTatMinutes);
        await AssertReconciledAsync(updated);
    }

    // ================================================================
    // Meeting-style TAT presentation (tatSummary / tatPausedMinutes) —
    // same TatSummaryCalculator formula Meeting itself uses.
    // ================================================================

    [Fact]
    public async Task TatSummary_BeforeStart_ShowsZeroUsedAndFullAllotted_TatPausedMinutesNull()
    {
        var type = await RuleAsync(240);
        var created = await CreateAsync(type);

        Assert.Equal(TimeSpan.Zero, created.TatSummary.Tat);
        Assert.Equal(TimeSpan.FromMinutes(240), created.TatSummary.TotalTat);
        Assert.Equal(TimeSpan.FromMinutes(240), created.TatSummary.TatDifference);
        Assert.Null(created.TatSummary.StartTime);
        Assert.Null(created.TatSummary.EndTime);
        Assert.Equal(TimeSpan.Zero, created.TatSummary.PauseTime);
        Assert.Equal(0, created.TatSummary.PauseCount);
        Assert.Null(created.TatPausedMinutes);
    }

    [Fact]
    public async Task TatSummary_Start_LiveTatMatchesTatUsedMinutes_TatPausedMinutesZero()
    {
        var type = await RuleAsync(240);
        var created = await CreateAsync(type);
        await Run(s => s.StartAsync(created.DelegationId));
        await BackdateStartAsync(created, 75);

        var view = await Get(created);

        Assert.Equal(75, (int)view.TatSummary.Tat!.Value.TotalMinutes);
        Assert.Equal(view.TatUsedMinutes, (int)view.TatSummary.Tat!.Value.TotalMinutes);
        Assert.Equal(0, view.TatPausedMinutes);
        Assert.Equal(0, view.TatSummary.PauseCount);
        Assert.NotNull(view.TatSummary.StartTime);
        Assert.Null(view.TatSummary.EndTime);
    }

    [Fact]
    public async Task TatSummary_Pause_FreezesLiveTat_ShowsPauseCountAndTatPausedMinutes()
    {
        var type = await RuleAsync(240);
        var created = await CreateAsync(type);
        await Run(s => s.StartAsync(created.DelegationId));
        await BackdateStartAsync(created, 90);

        await Run(s => s.PauseAsync(created.DelegationId, null));
        await SetPauseWindowAsync(created, 30, null);   // paused since minute 30: only 30 active minutes so far

        var view = await Get(created);
        Assert.True(view.IsPaused);
        Assert.Equal(EaTaskExecutionStatus.InProgress, view.ExecutionStatus);
        Assert.Equal(30, (int)view.TatSummary.Tat!.Value.TotalMinutes);
        Assert.Equal(view.TatUsedMinutes, (int)view.TatSummary.Tat!.Value.TotalMinutes);   // strict Meeting alignment: stays populated (frozen at 30) while paused, never null
        Assert.Equal(1, view.TatSummary.PauseCount);
        Assert.Equal(60, view.TatPausedMinutes);
        Assert.Equal(view.TatPausedMinutes, (int)view.TatSummary.PauseTime.TotalMinutes);
    }

    [Fact]
    public async Task TatSummary_Resume_ContinuesFromPriorActiveDuration_ExcludingPausedWindow()
    {
        var type = await RuleAsync(240);
        var created = await CreateAsync(type);
        await Run(s => s.StartAsync(created.DelegationId));
        await BackdateStartAsync(created, 180);
        await Run(s => s.PauseAsync(created.DelegationId, null));

        await Run(s => s.ResumeAsync(created.DelegationId));
        await SetPauseWindowAsync(created, 30, 90);   // a 60-minute pause inside 180 elapsed minutes

        var view = await Get(created);
        Assert.False(view.IsPaused);
        Assert.Equal(120, (int)view.TatSummary.Tat!.Value.TotalMinutes);   // 180 - 60; not restarted and not 180
        Assert.Equal(60, view.TatPausedMinutes);
        Assert.Equal(1, view.TatSummary.PauseCount);
    }

    [Fact]
    public async Task TatSummary_Complete_FreezesFinalTat_MatchesTatUsedMinutesAndEndTime()
    {
        var type = await RuleAsync(240);
        var created = await CreateAsync(type);
        await Run(s => s.StartAsync(created.DelegationId));
        await BackdateStartAsync(created, 180);
        await Run(s => s.PauseAsync(created.DelegationId, null));
        await Run(s => s.ResumeAsync(created.DelegationId));
        await SetPauseWindowAsync(created, 30, 90);

        var completed = await Run(s => s.CompleteAndApproveAsync(created.DelegationId, null));

        Assert.Equal(120, completed.TatUsedMinutes);                                      // stable once Completed
        Assert.Equal(120, (int)completed.TatSummary.Tat!.Value.TotalMinutes);              // matches tatSummary
        Assert.Equal(completed.CompletedAt, completed.TatSummary.EndTime);
        Assert.Equal(completed.CompletedAt, completed.TatSummary.LastActiveTime);
        Assert.Equal(60, completed.TatPausedMinutes);
        // totalTat(240:00:00 exactly) - used(120min + real test-overhead remainder): the remainder can
        // truncate the whole-minute difference either side of 120 (e.g. 120min02s used => 119, not 120)
        // — real TimeSpan truncation, not a bug — so this is a tolerance, not an exact-120 assertion.
        Assert.InRange((int)completed.TatSummary.TatDifference.TotalMinutes, 119, 120);

        var later = await Get(created);   // never increases again once frozen
        Assert.Equal(120, (int)later.TatSummary.Tat!.Value.TotalMinutes);
        Assert.Equal(120, later.TatUsedMinutes);
    }

    [Fact]
    public async Task NoTatDelegation_TatSummaryUsesMeetingsUnavailableShape_AtEveryStep()
    {
        var created = await CreateAsync(null);
        static void AssertUnavailable(DelegationResponseDto d)
        {
            Assert.Null(d.TatSummary.Tat);
            Assert.Equal(TimeSpan.Zero, d.TatSummary.TotalTat);
            Assert.Equal(TimeSpan.Zero, d.TatSummary.TatDifference);
            Assert.Equal(TimeSpan.Zero, d.TatSummary.PauseTime);
            Assert.Equal(0, d.TatSummary.PauseCount);
            Assert.Null(d.TatPausedMinutes);
            Assert.Null(d.TatUsedMinutes);
            Assert.Null(d.AllottedTatMinutes);
        }
        AssertUnavailable(created);

        var started = await Run(s => s.StartAsync(created.DelegationId));
        AssertUnavailable(started);
        await BackdateStartAsync(created, 50);
        var running = await Get(created);
        AssertUnavailable(running);
        var paused = await Run(s => s.PauseAsync(created.DelegationId, null));
        AssertUnavailable(paused);
        var resumed = await Run(s => s.ResumeAsync(created.DelegationId));
        AssertUnavailable(resumed);
        var completed = await Run(s => s.CompleteAndApproveAsync(created.DelegationId, null));
        AssertUnavailable(completed);
    }

    [Fact]
    public async Task List_PopulatesTatSummaryAndTatPausedMinutes_InOneBatch_NoTatRowsUseUnavailableShape()
    {
        var type = await RuleAsync(100);
        var running = await CreateAsync(type);
        await Run(s => s.StartAsync(running.DelegationId));
        await BackdateStartAsync(running, 40);
        var paused = await CreateAsync(type);
        await Run(s => s.StartAsync(paused.DelegationId));
        await BackdateStartAsync(paused, 40);
        await Run(s => s.PauseAsync(paused.DelegationId, null));
        await SetPauseWindowAsync(paused, 10, null);
        var noTat = await CreateAsync(null);

        var items = (await Run2(s => s.ListAsync(new DelegationListQueryDto { DoerId = "EMP-C", PageSize = 200 }))).Items;
        DelegationResponseDto Row(DelegationResponseDto d) => items.Single(i => i.DelegationId == d.DelegationId);

        Assert.Equal(40, (int)Row(running).TatSummary.Tat!.Value.TotalMinutes);
        Assert.Equal(0, Row(running).TatPausedMinutes);
        Assert.Equal(10, (int)Row(paused).TatSummary.Tat!.Value.TotalMinutes);
        Assert.Equal(30, Row(paused).TatPausedMinutes);
        Assert.Null(Row(noTat).TatSummary.Tat);
        Assert.Null(Row(noTat).TatPausedMinutes);
    }
}
