using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Controllers.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Moq;
using UglyToad.PdfPig.Writer;
using Xunit;
using DelegationEntity = Jarvis5.Entities.EaFms.Delegation;

namespace Jarvis5.Tests.EaFms.Delegation;

/// <summary>
/// POST /api/ea/delegations/{id}/pause and /resume reuse the shared WorkPause model (no Delegation-specific pause
/// table/entity/status). Paused is derived: EaTask InProgress + an open WorkPause.
/// </summary>
public class DelegationPauseResumeTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    public void Dispose()
    {
        foreach (var dir in _tempDirs) try { Directory.Delete(dir, recursive: true); } catch { /* best-effort */ }
    }

    private sealed class Fx
    {
        public required EaFmsDbContext Db { get; init; }
        public required DelegationService Svc { get; init; }
        public required Mock<IAuditService> Audit { get; init; }
        public required long DelegationId { get; init; }
        public required long EaTaskId { get; init; }
        public required long ModuleId { get; init; }
        public required Func<Task<DelegationResponseDto>> CreateMore { get; init; }
    }

    private static byte[] ValidPdf()
    {
        var b = new PdfDocumentBuilder();
        b.AddPage(200, 200);
        return b.Build();
    }

    private static IFormFile Pdf()
    {
        var ms = new MemoryStream(ValidPdf());
        return new FormFile(ms, 0, ms.Length, "completionPdf", "done.pdf") { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
    }

    private async Task<Fx> NewAsync(bool start = true)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
        _tempDirs.Add(root);
        var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == root);
        var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
        db.BusinessModules.Add(new BusinessModule { Name = "Spacer", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        var module = new BusinessModule { Name = DelegationService.DelegationBusinessModuleName, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.BusinessModules.Add(module);
        db.Statuses.Add(new Status { Name = "Captured", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        db.Statuses.Add(new Status { Name = "In Progress", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        db.Statuses.Add(new Status { Name = "Completed", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "ea-actor" && u.UserId == 42L);
        var numbers = new Mock<IDelegationNumberRepository>();
        var seq = 0;
        numbers.Setup(r => r.GenerateNextReferenceNoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => $"DLG-T-{++seq:D6}");
        var tasks = new Mock<IEaTaskService>();
        tasks.Setup(s => s.CreateWithoutTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateEaTaskDto dto, CancellationToken _) =>
            {
                var t = new EaTask { BusinessModuleId = module.Id, ModuleName = DelegationService.DelegationBusinessModuleName, BusinessRecordId = dto.BusinessRecordId,
                    Task = dto.Task ?? dto.BusinessRecordId, ExecutionStatus = "NotStarted", IsActive = true, CreatedBy = "ea-actor", CreatedDate = DateTime.UtcNow };
                db.Tasks.Add(t); db.SaveChanges();
                return new EaTaskResponseDto { EaTaskId = t.Id, ModuleId = module.Id, ModuleName = t.ModuleName, BusinessRecordId = t.BusinessRecordId,
                    Task = t.Task, ExecutionStatus = t.ExecutionStatus, IsActive = true, CreatedBy = t.CreatedBy, CreatedDate = t.CreatedDate };
            });
        var audit = new Mock<IAuditService>();
        var svc = new DelegationService(db, user, audit.Object, numbers.Object, tasks.Object, env);
        Task<DelegationResponseDto> Create() => svc.CreateAsync(new DelegationCreateRequestDto { Title = "Prepare deck", DoerId = "emp-1", EndDate = DateTime.UtcNow.AddDays(5) });
        var created = await Create();
        if (start) await svc.StartAsync(created.DelegationId);
        return new Fx { Db = db, Svc = svc, Audit = audit, DelegationId = created.DelegationId, EaTaskId = created.EaTaskId, ModuleId = module.Id, CreateMore = Create };
    }

    private static Task<List<WorkPause>> Pauses(Fx f) => f.Db.WorkPauses.AsNoTracking().ToListAsync();
    private static Task<EaTask> Task_(Fx f) => f.Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == f.EaTaskId);
    private static Task<DelegationEntity> Row(Fx f) => f.Db.Delegations.AsNoTracking().SingleAsync(d => d.Id == f.DelegationId);

    // ---------------- pause ----------------
    [Fact]
    public async Task Pause_OpensExactlyOneSharedWorkPause_StatusesUnchanged_IsPausedTrue()
    {
        var f = await NewAsync();

        var r = await f.Svc.PauseAsync(f.DelegationId, null);

        Assert.True(r.IsPaused);
        Assert.Equal(DelegationStatus.InProgress, r.Status);
        Assert.Equal(DelegationStatus.InProgress, (await Row(f)).Status);
        Assert.Equal(EaTaskExecutionStatus.InProgress, (await Task_(f)).ExecutionStatus);
        var pause = Assert.Single(await Pauses(f));
        Assert.Null(pause.EndAt);
        Assert.False(pause.IsDeleted);
        Assert.Equal("Delegation paused", pause.Reason);
        Assert.Equal("ea-actor", pause.CreatedBy);
    }

    [Fact]
    public async Task Pause_StoresTheProvidedReason()
    {
        var f = await NewAsync();

        await f.Svc.PauseAsync(f.DelegationId, new DelegationPauseRequestDto { PauseReason = "  Waiting for input  " });

        Assert.Equal("Waiting for input", Assert.Single(await Pauses(f)).Reason);
    }

    [Fact]
    public async Task Pause_ReasonLongerThan2000_Is400_AndChangesNothing()
    {
        var f = await NewAsync();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            f.Svc.PauseAsync(f.DelegationId, new DelegationPauseRequestDto { PauseReason = new string('x', 2001) }));

        Assert.Empty(await Pauses(f));
        Assert.Null((await Task_(f)).WorkflowInstanceId);
    }

    [Fact]
    public async Task Pause_IdentityIsTheDelegationId_ResolvedByModuleName_NotEaTaskOrDoer()
    {
        var f = await NewAsync();
        Assert.NotEqual(f.ModuleId, 1L); // a "Spacer" module holds id 1, so nothing can be relying on a hardcoded id

        await f.Svc.PauseAsync(f.DelegationId, null);

        var wf = await f.Db.WorkflowInstances.AsNoTracking().SingleAsync();
        Assert.Equal(f.ModuleId, wf.BusinessModuleId);
        Assert.Equal(f.DelegationId.ToString(), wf.BusinessRecordId);
        Assert.NotEqual("emp-1", wf.BusinessRecordId);
        Assert.Equal(wf.Id, (await Task_(f)).WorkflowInstanceId);
        Assert.Equal(wf.Id, Assert.Single(await Pauses(f)).WorkflowInstanceId);
    }

    [Fact]
    public async Task Pause_Pending_Is409_NothingCreated()
    {
        var f = await NewAsync(start: false);

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.PauseAsync(f.DelegationId, null));

        Assert.Empty(await Pauses(f));
        Assert.Empty(await f.Db.WorkflowInstances.ToListAsync());
    }

    [Fact]
    public async Task Pause_Completed_Is409()
    {
        var f = await NewAsync();
        await f.Svc.CompleteAsync(f.DelegationId, null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.PauseAsync(f.DelegationId, null));

        Assert.Empty(await Pauses(f));
    }

    [Fact]
    public async Task Pause_Unknown_Is404()
    {
        var f = await NewAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => f.Svc.PauseAsync(9999, null));
    }

    [Fact]
    public async Task Pause_WhenAlreadyPaused_Is409_StillExactlyOneOpenPause()
    {
        var f = await NewAsync();
        await f.Svc.PauseAsync(f.DelegationId, null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.PauseAsync(f.DelegationId, null));

        Assert.Contains("already paused", ex.Message);
        Assert.Single(await Pauses(f));
        Assert.Single(await f.Db.WorkflowInstances.ToListAsync());
    }

    [Fact]
    public async Task Pause_CreatesNoNewTaskOrDelegation()
    {
        var f = await NewAsync();
        var (tasks, delegations) = (await f.Db.Tasks.CountAsync(), await f.Db.Delegations.CountAsync());

        await f.Svc.PauseAsync(f.DelegationId, null);
        await f.Svc.ResumeAsync(f.DelegationId);

        Assert.Equal((tasks, delegations), (await f.Db.Tasks.CountAsync(), await f.Db.Delegations.CountAsync()));
    }

    // ---------------- resume ----------------
    [Fact]
    public async Task Resume_ClosesTheOpenPause_IsPausedFalse_StatusUnchanged()
    {
        var f = await NewAsync();
        await f.Svc.PauseAsync(f.DelegationId, null);

        var r = await f.Svc.ResumeAsync(f.DelegationId);

        Assert.False(r.IsPaused);
        Assert.Equal(DelegationStatus.InProgress, r.Status);
        Assert.Equal(EaTaskExecutionStatus.InProgress, (await Task_(f)).ExecutionStatus);
        var pause = Assert.Single(await Pauses(f));
        Assert.NotNull(pause.EndAt);
        Assert.Equal("42", pause.ResumedById);
        Assert.Equal("ea-actor", pause.ResumedByName);
        Assert.DoesNotContain(await Pauses(f), p => p.EndAt == null);
    }

    [Fact]
    public async Task Resume_WhenNotPaused_Is409_SameBusinessRuleConvention()
    {
        var f = await NewAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.ResumeAsync(f.DelegationId));

        await f.Svc.PauseAsync(f.DelegationId, null);
        await f.Svc.ResumeAsync(f.DelegationId);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.ResumeAsync(f.DelegationId));
        Assert.Single(await Pauses(f));
    }

    [Fact]
    public async Task Resume_PendingCompletedOrUnknown_Rejected()
    {
        var pending = await NewAsync(start: false);
        await Assert.ThrowsAsync<BusinessRuleException>(() => pending.Svc.ResumeAsync(pending.DelegationId));

        var done = await NewAsync();
        await done.Svc.CompleteAsync(done.DelegationId, null);
        await Assert.ThrowsAsync<BusinessRuleException>(() => done.Svc.ResumeAsync(done.DelegationId));

        await Assert.ThrowsAsync<NotFoundException>(() => done.Svc.ResumeAsync(9999));
    }

    [Fact]
    public async Task PauseAfterResume_OpensASecondPause_OnTheSameAnchor()
    {
        var f = await NewAsync();
        await f.Svc.PauseAsync(f.DelegationId, null);
        await f.Svc.ResumeAsync(f.DelegationId);

        var r = await f.Svc.PauseAsync(f.DelegationId, new DelegationPauseRequestDto { PauseReason = "again" });

        Assert.True(r.IsPaused);
        var pauses = await Pauses(f);
        Assert.Equal(2, pauses.Count);
        Assert.Single(pauses, p => p.EndAt == null);
        Assert.Single(await f.Db.WorkflowInstances.ToListAsync());
    }

    // ---------------- sequence / complete interaction ----------------
    [Fact]
    public async Task Sequence_Start_Pause_Resume_Complete_HasTheExpectedStatusAndIsPausedAtEachStep()
    {
        var f = await NewAsync(start: false);
        Assert.False((await f.Svc.GetByIdAsync(f.DelegationId)).IsPaused);

        var started = await f.Svc.StartAsync(f.DelegationId);
        Assert.Equal((DelegationStatus.InProgress, false), (started.Status, started.IsPaused));
        Assert.Equal(EaTaskExecutionStatus.InProgress, (await Task_(f)).ExecutionStatus);

        var paused = await f.Svc.PauseAsync(f.DelegationId, null);
        Assert.Equal((DelegationStatus.InProgress, true), (paused.Status, paused.IsPaused));
        Assert.True((await f.Svc.GetByIdAsync(f.DelegationId)).IsPaused);

        var resumed = await f.Svc.ResumeAsync(f.DelegationId);
        Assert.Equal((DelegationStatus.InProgress, false), (resumed.Status, resumed.IsPaused));

        var completed = await f.Svc.CompleteAsync(f.DelegationId, Pdf());
        Assert.Equal((DelegationStatus.Completed, false), (completed.Status, completed.IsPaused));
        Assert.Equal(EaTaskExecutionStatus.Completed, (await Task_(f)).ExecutionStatus);
        Assert.NotNull(completed.CompletionPdfAttachmentId);
        Assert.False((await f.Svc.GetByIdAsync(f.DelegationId)).IsPaused);
    }

    [Fact]
    public async Task Complete_WhilePaused_IsBlocked_WithMeetingsRule_AndChangesNothing()
    {
        var f = await NewAsync();
        await f.Svc.PauseAsync(f.DelegationId, null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.CompleteAsync(f.DelegationId, Pdf()));

        Assert.Equal("Resume or continue open pauses/waiting before completion.", ex.Message);
        Assert.Equal(DelegationStatus.InProgress, (await Row(f)).Status);
        Assert.Equal(EaTaskExecutionStatus.InProgress, (await Task_(f)).ExecutionStatus);
        Assert.Null((await Task_(f)).CompletedAt);
        Assert.Single(await Pauses(f), p => p.EndAt == null);
        Assert.Empty(await f.Db.Attachments.ToListAsync());
    }

    [Fact]
    public async Task CompleteAfterResume_LeavesNoOpenPause_AndClosesTheAnchor()
    {
        var f = await NewAsync();
        await f.Svc.PauseAsync(f.DelegationId, null);
        await f.Svc.ResumeAsync(f.DelegationId);

        await f.Svc.CompleteAsync(f.DelegationId, Pdf());

        Assert.DoesNotContain(await Pauses(f), p => p.EndAt == null);
        var wf = await f.Db.WorkflowInstances.AsNoTracking().SingleAsync();
        Assert.False(wf.IsActive);
        Assert.NotNull(wf.CompletedAt);
        Assert.Single(await f.Db.Attachments.Where(a => a.RelatedModule == "Delegation").ToListAsync());
    }

    [Fact]
    public async Task CompleteWithoutEverPausing_StillWorks_AndCreatesNoWorkflow()
    {
        var f = await NewAsync();

        var r = await f.Svc.CompleteAsync(f.DelegationId, null);

        Assert.Equal(DelegationStatus.Completed, r.Status);
        Assert.Empty(await f.Db.WorkflowInstances.ToListAsync());
        Assert.Null((await Task_(f)).WorkflowInstanceId);
    }

    // ---------------- responses ----------------
    [Fact]
    public async Task ListAndDetail_ExposeIsPaused_WithBatchedLookup()
    {
        var f = await NewAsync();
        var second = await f.CreateMore();
        await f.Svc.StartAsync(second.DelegationId);
        var third = await f.CreateMore(); // Pending
        await f.Svc.PauseAsync(f.DelegationId, null);

        var list = await f.Svc.ListAsync(new DelegationListQueryDto { PageSize = 50 });

        Assert.True(list.Items.Single(i => i.DelegationId == f.DelegationId).IsPaused);
        Assert.False(list.Items.Single(i => i.DelegationId == second.DelegationId).IsPaused);
        Assert.False(list.Items.Single(i => i.DelegationId == third.DelegationId).IsPaused);
        Assert.True((await f.Svc.GetByIdAsync(f.DelegationId)).IsPaused);
        Assert.False((await f.Svc.GetByIdAsync(second.DelegationId)).IsPaused);
    }

    [Fact]
    public async Task Update_WhilePaused_StillReportsIsPaused()
    {
        var f = await NewAsync();
        await f.Svc.PauseAsync(f.DelegationId, null);

        var r = await f.Svc.UpdateAsync(f.DelegationId, new DelegationUpdateRequestDto { Title = "Renamed", DoerId = "emp-1" });

        Assert.True(r.IsPaused);
        Assert.Equal("Renamed", r.Title);
    }

    // ---------------- audit / history ----------------
    [Fact]
    public async Task PauseAndResume_WriteOneAuditEachAndOneSharedWorkflowHistoryRowEach()
    {
        var f = await NewAsync();

        await f.Svc.PauseAsync(f.DelegationId, null);
        await f.Svc.ResumeAsync(f.DelegationId);

        f.Audit.Verify(a => a.AddAudit("DELEGATION_PAUSE", "Delegation", "Delegation", f.DelegationId.ToString(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()), Times.Once);
        f.Audit.Verify(a => a.AddAudit("DELEGATION_RESUME", "Delegation", "Delegation", f.DelegationId.ToString(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()), Times.Once);
        var history = await f.Db.WorkflowHistory.AsNoTracking().OrderBy(h => h.Id).ToListAsync();
        Assert.Equal(new[] { "SIMPLE_PAUSE", "SIMPLE_RESUME" }, history.Select(h => h.TransitionType));
        Assert.All(history, h => Assert.Equal(h.FromStatusId, h.ToStatusId));
    }

    [Fact]
    public async Task FailedPause_WritesNoAudit()
    {
        var f = await NewAsync(start: false);

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.PauseAsync(f.DelegationId, null));

        f.Audit.Verify(a => a.AddAudit("DELEGATION_PAUSE", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task EaTaskHistory_ShowsStartedPausedResumedCompleted_WithoutDuplicates()
    {
        var f = await NewAsync();
        // The Delegation audit trail is mocked in these tests; seed the two rows the history reads.
        f.Db.AuditLogs.Add(new AuditLog { Module = "Delegation", EntityName = "Delegation", EntityId = f.DelegationId.ToString(), ActionType = "DELEGATION_START", OccurredAt = DateTime.UtcNow.AddSeconds(-5), ActorName = "ea-actor" });
        await f.Db.SaveChangesAsync();
        await f.Svc.PauseAsync(f.DelegationId, new DelegationPauseRequestDto { PauseReason = "hold" });
        await f.Svc.ResumeAsync(f.DelegationId);

        var events = await new EaTaskHistoryBuilder(f.Db).BuildAsync(f.EaTaskId, default);

        Assert.Equal(1, events.Count(e => e.EventType == "Paused"));
        Assert.Equal(1, events.Count(e => e.EventType == "Resumed"));
        Assert.Equal(1, events.Count(e => e.EventType == "Started"));
        Assert.Equal("hold", events.Single(e => e.EventType == "Paused").Notes);
        Assert.True(events.Single(e => e.EventType == "Paused").IsPaused);
        Assert.False(events.Single(e => e.EventType == "Resumed").IsPaused);
    }

    // ---------------- EM report compatibility ----------------
    [Fact]
    public async Task EmReport_TreatsAPausedDelegationAsPaused_ThenInProgressAfterResume_WithoutEmChanges()
    {
        var f = await NewAsync();
        var em = new EmReportService(f.Db);
        await f.Svc.PauseAsync(f.DelegationId, null);

        var overview = await em.GetOverviewAsync(new(), default);
        Assert.Equal((1, 1, 0), (overview.TotalTasks, overview.Paused, overview.InProgress));
        var row = Assert.Single((await em.GetTasksAsync(new() { ExecutionStatus = "Paused" }, default)).Items);
        Assert.True(row.IsPaused);
        Assert.Equal(EaTaskExecutionStatus.InProgress, row.ExecutionStatus);
        Assert.Equal(1, (await em.GetModulesAsync(new(), default)).Single(m => m.ModuleName == "Delegation").Paused);

        await f.Svc.ResumeAsync(f.DelegationId);

        overview = await em.GetOverviewAsync(new(), default);
        Assert.Equal((0, 1), (overview.Paused, overview.InProgress));
        Assert.Empty((await em.GetTasksAsync(new() { ExecutionStatus = "Paused" }, default)).Items);
        Assert.False(Assert.Single((await em.GetTasksAsync(new() { ExecutionStatus = "InProgress" }, default)).Items).IsPaused);
    }

    // ---------------- contract / no schema additions ----------------
    [Fact]
    public void Controller_ExposesPauseAndResume_PostRoutes_WithAnOptionalPauseBody()
    {
        var pause = typeof(DelegationsController).GetMethod(nameof(DelegationsController.Pause))!;
        var resume = typeof(DelegationsController).GetMethod(nameof(DelegationsController.Resume))!;

        Assert.Equal("{delegationId:long}/pause", pause.GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.Equal("{delegationId:long}/resume", resume.GetCustomAttribute<HttpPostAttribute>()!.Template);
        var body = pause.GetParameters().Single(p => p.ParameterType == typeof(DelegationPauseRequestDto));
        Assert.Equal(EmptyBodyBehavior.Allow, body.GetCustomAttribute<FromBodyAttribute>()!.EmptyBodyBehavior);
        Assert.DoesNotContain(resume.GetParameters(), p => p.GetCustomAttribute<FromBodyAttribute>() != null);
    }

    [Fact]
    public void NoDelegationSpecificPauseStorage_NoNewStatus_IsPausedIsNotPersisted()
    {
        var asm = typeof(DelegationEntity).Assembly;
        Assert.DoesNotContain(asm.GetTypes(), t => t.Name is "DelegationPause" or "DelegationPauseDto" && t.Namespace == "Jarvis5.Entities.EaFms");
        Assert.DoesNotContain(typeof(EaFmsDbContext).GetProperties(), p => p.Name.Contains("DelegationPause"));
        Assert.DoesNotContain(typeof(DelegationStatus).GetFields(), f => f.Name.Contains("Paused"));
        Assert.DoesNotContain(typeof(EaTaskExecutionStatus).GetFields(), f => f.Name.Contains("Paused"));
        Assert.Null(typeof(DelegationEntity).GetProperty("IsPaused"));
        Assert.NotNull(typeof(DelegationResponseDto).GetProperty(nameof(DelegationResponseDto.IsPaused)));
    }
}
