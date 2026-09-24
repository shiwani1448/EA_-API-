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
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Approvals;

/// <summary>
/// EA Approval's TAT/Review/Rework phase tracking (ApprovalPhaseTat) brought up to parity with
/// Delegation's. Covers: Actual phase opens at creation, submit-for-review/review-approve/review-rework
/// phase transitions, pause/resume gating, business Approve freezing the open phase + pause anchor, and
/// the new GetApplicableForApprovalPhaseAsync exact-match/fallback resolution.
/// </summary>
public class ApprovalPhaseTatTests
{
    private sealed class Fx
    {
        public required EaFmsDbContext Db { get; init; }
        public required Jarvis5.Services.EaFms.ApprovalService Service { get; init; }
        public required ApprovalLifecycleService Lifecycle { get; init; }
        public required ApprovalQueryService Queries { get; init; }
        public required long ModuleId { get; init; }
    }

    private static async Task<Fx> NewAsync()
    {
        var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
        var module = new BusinessModule { Name = "EA Approval", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.BusinessModules.Add(module);
        db.Statuses.Add(new Status { Name = "In Progress", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        db.Statuses.Add(new Status { Name = "Completed", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var numbers = new Mock<IApprovalNumberRepository>();
        var n = 0;
        numbers.Setup(r => r.GenerateNextReferenceNoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => $"APR-PT-{++n:D4}");
        var tasks = new Mock<IEaTaskService>();
        tasks.Setup(s => s.CreateWithoutTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateEaTaskDto d, CancellationToken _) =>
            {
                var t = new EaTask { BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = d.BusinessRecordId, Task = d.Task,
                    ExecutionStatus = EaTaskExecutionStatus.NotStarted, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
                db.Tasks.Add(t); db.SaveChanges();
                return new EaTaskResponseDto { EaTaskId = t.Id, ModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = t.BusinessRecordId,
                    Task = t.Task, ExecutionStatus = t.ExecutionStatus, IsActive = true, CreatedBy = "seed", CreatedDate = t.CreatedDate };
            });

        var audit = Mock.Of<IAuditService>();
        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "phase-tat-actor" && u.UserId == 71L);
        var taskReview = new TaskReviewService(db, new TaskReviewRepository(db), user, audit);
        var tatRules = new TatRuleRepository(db);
        var service = new Jarvis5.Services.EaFms.ApprovalService(db, audit, numbers.Object, tasks.Object, tatRules);

        var documents = new Mock<IApprovalDocumentService>();
        var queries = new ApprovalQueryService(db, documents.Object, taskReview, tatRules);
        var spMock = new Mock<IServiceProvider>();
        spMock.Setup(s => s.GetService(typeof(ApprovalQueryService))).Returns(queries);
        var lifecycle = new ApprovalLifecycleService(db, audit, taskReview, user,
            Mock.Of<IWebHostEnvironment>(), spMock.Object, tatRules);

        return new Fx { Db = db, Service = service, Lifecycle = lifecycle, Queries = queries, ModuleId = module.Id };
    }

    [Fact]
    public void NullableStart_MigrationAndSnapshotMatchModel()
    {
        using var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseNpgsql("Host=localhost;Database=model_validation;Username=unused;Password=unused").Options);
        var assembly = db.GetService<IMigrationsAssembly>();
        var snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(assembly.ModelSnapshot!.Model, designTime: true);
        var model = db.GetService<IDesignTimeModel>().Model;
        Assert.Empty(db.GetService<IMigrationsModelDiffer>().GetDifferences(snapshot.GetRelationalModel(), model.GetRelationalModel()));
        var migration = assembly.CreateMigration(assembly.Migrations["20260923120000_MakeApprovalPhaseTatStartedAtNullable"], db.Database.ProviderName!);
        var change = Assert.IsType<AlterColumnOperation>(Assert.Single(migration.UpOperations));
        Assert.Equal("ea_approval_phase_tat", change.Table);
        Assert.Equal("StartedAt", change.Name);
        Assert.True(change.IsNullable);
        Assert.False(change.OldColumn.IsNullable);
        Assert.False(Assert.IsType<AlterColumnOperation>(Assert.Single(migration.DownOperations)).IsNullable);
        var target = db.GetService<IModelRuntimeInitializer>().Initialize(migration.TargetModel, designTime: true);
        // Later migrations may add unrelated entities/columns; this historical migration must
        // retain the nullable Approval phase start. The current snapshot is checked above.
        Assert.True(target.FindEntityType(typeof(ApprovalPhaseTat))!.FindProperty(nameof(ApprovalPhaseTat.StartedAt))!.IsNullable);
    }

    [Fact]
    public async Task ExplicitStarts_AllPhases_OpenIdle_RejectWrongOrRepeatedStarts()
    {
        var f = await NewAsync();
        await AddRuleAsync(f.Db, f.ModuleId, "Finance", null, null, 90);
        var created = await f.Service.CreateAsync(new ApprovalRequest { RequestTitle = "Explicit starts", RequestType = "Finance", CreatedBy = "creator" });
        var idle = await f.Queries.DetailAsync(created.Id, default);
        Assert.Null(Assert.Single(idle!.PhaseTat).StartedAt);
        Assert.Equal(0, idle.TatUsedMinutes);
        Assert.Equal(TimeSpan.FromMinutes(90), idle.TatSummary.TatDifference);
        Assert.Null(idle.TatSummary.StartTime);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.StartReviewAsync(created.Id));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.StartReworkAsync(created.Id));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.SubmitForReviewAsync(created.Id, new()));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.PauseAsync(created.Id, null));
        Assert.Empty(await f.Db.TaskReviews.ToListAsync());
        Assert.Empty(await f.Db.WorkflowInstances.ToListAsync());

        var started = await f.Lifecycle.StartActualAsync(created.Id);
        var task = await f.Db.Tasks.SingleAsync();
        Assert.Equal(EaTaskExecutionStatus.InProgress, task.ExecutionStatus);
        Assert.Equal(task.StartedAt, Assert.Single(started.PhaseTat).StartedAt);
        Assert.NotNull(task.StartedAt);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.StartActualAsync(created.Id));
        await f.Lifecycle.SubmitForReviewAsync(created.Id, new());
        var review = await f.Queries.DetailAsync(created.Id, default);
        Assert.Null(review!.PhaseTat.Last().StartedAt);
        Assert.Equal(0, review.TatUsedMinutes);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.PauseAsync(created.Id, null));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.StartReworkAsync(created.Id));
        await f.Lifecycle.StartReviewAsync(created.Id);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.StartReviewAsync(created.Id));
        await f.Lifecycle.RequestTaskReworkAsync(created.Id, new() { ReworkRemark = "fix" }, null);
        var rework = await f.Queries.DetailAsync(created.Id, default);
        Assert.Null(rework!.PhaseTat.Last().StartedAt);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.PauseAsync(created.Id, null));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.SubmitForReviewAsync(created.Id, new()));
        await f.Lifecycle.StartReworkAsync(created.Id);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.StartReworkAsync(created.Id));
        await f.Lifecycle.SubmitForReviewAsync(created.Id, new());
        Assert.Equal(task.StartedAt, started.PhaseTat[0].StartedAt);
        await f.Lifecycle.ApproveReviewAsync(created.Id, new(), null);
        var closedIdle = (await f.Queries.DetailAsync(created.Id, default))!.PhaseTat.Last();
        Assert.Null(closedIdle.StartedAt);
        Assert.NotNull(closedIdle.EndedAt);
        Assert.Equal(0m, closedIdle.TatUsedSeconds);
        Assert.Equal(0, closedIdle.PauseCount);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task BusinessDecision_WithoutStart_FreezesZeroSnapshot(bool approve, bool withTat)
    {
        var f = await NewAsync();
        if (withTat) await AddRuleAsync(f.Db, f.ModuleId, null, null, null, 90);
        var created = await f.Service.CreateAsync(new ApprovalRequest { RequestTitle = "Idle decision", CreatedBy = "creator" });
        await f.Lifecycle.RequestChangesAsync(created.Id, new() { Comment = "details" });
        await f.Lifecycle.ResubmitAsync(created.Id);
        Assert.Equal(EaTaskExecutionStatus.NotStarted, (await f.Db.Tasks.SingleAsync()).ExecutionStatus);
        if (approve) await f.Lifecycle.ApproveAsync(created.Id, new());
        else await f.Lifecycle.RejectAsync(created.Id, new() { Comment = "no" });
        var phase = await f.Db.ApprovalPhaseTats.SingleAsync();
        Assert.Null(phase.StartedAt);
        Assert.NotNull(phase.EndedAt);
        Assert.Equal(withTat ? 0 : (int?)null, phase.TatUsedMinutes);
        Assert.Equal(withTat ? 0m : (decimal?)null, phase.TatUsedSeconds);
        Assert.Equal(0, phase.TatPausedMinutes);
        Assert.Equal(0m, phase.TatPausedSeconds);
        Assert.Equal(0, phase.PauseCount);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.StartActualAsync(created.Id));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.StartReviewAsync(created.Id));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.StartReworkAsync(created.Id));
    }

    [Theory]
    [InlineData(DelegationTaskType.Actual)]
    [InlineData(DelegationTaskType.Review)]
    [InlineData(DelegationTaskType.Rework)]
    public async Task Start_RejectsOpenPause_AndCancelledTask(string type)
    {
        var f = await NewAsync();
        var created = await f.Service.CreateAsync(new ApprovalRequest { RequestTitle = "Blocked start", CreatedBy = "creator" });
        var task = await f.Db.Tasks.SingleAsync();
        var phase = await f.Db.ApprovalPhaseTats.SingleAsync();
        phase.TaskType = type;
        task.ExecutionStatus = type == DelegationTaskType.Actual ? EaTaskExecutionStatus.NotStarted : EaTaskExecutionStatus.InProgress;
        task.WorkflowInstanceId = 999;
        var pause = new WorkPause { WorkflowInstanceId = 999, StartAt = Clock.UtcNowTz, CreatedBy = "seed", CreatedDate = Clock.UtcNowTz };
        f.Db.WorkPauses.Add(pause);
        await f.Db.SaveChangesAsync();
        Task<ApprovalDetailDto> Start() => type switch
        {
            DelegationTaskType.Actual => f.Lifecycle.StartActualAsync(created.Id),
            DelegationTaskType.Review => f.Lifecycle.StartReviewAsync(created.Id),
            _ => f.Lifecycle.StartReworkAsync(created.Id)
        };
        await Assert.ThrowsAsync<BusinessRuleException>(Start);
        Assert.Null(phase.StartedAt);
        pause.EndAt = Clock.UtcNowTz;
        task.ExecutionStatus = EaTaskExecutionStatus.Cancelled;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(Start);
        Assert.Null(phase.StartedAt);
    }

    [Fact]
    public async Task Start_UnknownApproval_ReturnsNotFound()
    {
        var f = await NewAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => f.Lifecycle.StartActualAsync(999));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Lifecycle.StartReviewAsync(999));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Lifecycle.StartReworkAsync(999));
    }

    private static async Task<TatRule> AddRuleAsync(EaFmsDbContext db, long moduleId, string? type, string? subtype, string? taskType, int minutes)
    {
        var rule = new TatRule
        {
            BusinessModuleId = moduleId, ModuleName = "EA Approval", Type = type, Subtype = subtype, TaskType = taskType,
            TatMinutes = minutes, IsActive = true, IsDeleted = false, CreatedBy = "seed", CreatedDate = DateTime.UtcNow
        };
        db.TatRules.Add(rule);
        await db.SaveChangesAsync();
        return rule;
    }

    // ============================================================
    // §4 — Actual phase opens at creation
    // ============================================================

    [Fact]
    public async Task Create_OpensActualPhaseIdle_WithResolvedTat()
    {
        var f = await NewAsync();
        var rule = await AddRuleAsync(f.Db, f.ModuleId, "Finance", null, DelegationTaskType.Actual, 90);

        var before = Clock.UtcNowTz;
        var created = await f.Service.CreateAsync(new ApprovalRequest { RequestTitle = "Capex", RequestType = "Finance", CreatedBy = "creator" });
        var after = Clock.UtcNowTz;

        var phase = Assert.Single(await f.Db.ApprovalPhaseTats.Where(p => p.ApprovalRequestId == created.Id).ToListAsync());
        Assert.Equal(DelegationTaskType.Actual, phase.TaskType);
        Assert.Equal(0, phase.ReviewCycleNumber);
        Assert.Null(phase.EndedAt);
        Assert.Null(phase.StartedAt);
        Assert.Equal(EaTaskExecutionStatus.NotStarted, (await f.Db.Tasks.SingleAsync()).ExecutionStatus);
        Assert.Null((await f.Db.Tasks.SingleAsync()).StartedAt);
        Assert.Equal("PendingApproval", created.WorkflowStatus);
        Assert.Equal(90, phase.AllottedTatMinutes);
        Assert.Equal(rule.Id, phase.TatRuleId);
    }

    [Fact]
    public async Task Create_NoMatchingRule_OpensActualPhase_WithNullAllotted_NeverThrows()
    {
        var f = await NewAsync();

        var created = await f.Service.CreateAsync(new ApprovalRequest { RequestTitle = "No rule", CreatedBy = "creator" });

        var phase = Assert.Single(await f.Db.ApprovalPhaseTats.Where(p => p.ApprovalRequestId == created.Id).ToListAsync());
        Assert.Null(phase.AllottedTatMinutes);
        Assert.Null(phase.TatRuleId);
    }

    // ============================================================
    // Task Review phase transitions
    // ============================================================

    [Fact]
    public async Task SubmitForReview_ClosesActualPhase_AndOpensReviewPhase()
    {
        var f = await NewAsync();
        var created = await f.Service.CreateAsync(new ApprovalRequest { RequestTitle = "Review flow", CreatedBy = "creator" });
        await f.Lifecycle.StartActualAsync(created.Id);

        var summary = await f.Lifecycle.SubmitForReviewAsync(created.Id, new SubmitForReviewRequestDto { ReviewerId = "rev-1" });

        var phases = await f.Db.ApprovalPhaseTats.Where(p => p.ApprovalRequestId == created.Id).OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(2, phases.Count);
        Assert.Equal(DelegationTaskType.Actual, phases[0].TaskType);
        Assert.NotNull(phases[0].EndedAt);
        Assert.Equal(DelegationTaskType.Review, phases[1].TaskType);
        Assert.Equal(1, phases[1].ReviewCycleNumber);
        Assert.Equal(summary.ReviewCycleNumber, phases[1].ReviewCycleNumber);
        Assert.Null(phases[1].EndedAt);
    }

    [Fact]
    public async Task ReviewApprove_ClosesReviewPhase_AndDoesNotTouchWorkflowStatusOrExecutionStatus()
    {
        var f = await NewAsync();
        var created = await f.Service.CreateAsync(new ApprovalRequest { RequestTitle = "Approve flow", CreatedBy = "creator" });
        await f.Lifecycle.StartActualAsync(created.Id);
        await f.Lifecycle.SubmitForReviewAsync(created.Id, new SubmitForReviewRequestDto());

        var approved = await f.Lifecycle.ApproveReviewAsync(created.Id, new ApproveTaskReviewRequestDto { ReviewRemark = "ok" }, null);

        Assert.Equal(TaskReviewStatus.Approved, approved.Status);
        var phases = await f.Db.ApprovalPhaseTats.Where(p => p.ApprovalRequestId == created.Id).OrderBy(p => p.Id).ToListAsync();
        var reviewPhase = phases.Single(p => p.TaskType == DelegationTaskType.Review);
        Assert.NotNull(reviewPhase.EndedAt);
        // Nothing new opens after an approved review — still just Actual (closed) + Review (closed).
        Assert.Equal(2, phases.Count);
        Assert.All(phases, p => Assert.NotNull(p.EndedAt));

        var req = await f.Db.ApprovalRequests.AsNoTracking().SingleAsync(a => a.Id == created.Id);
        Assert.Equal("PendingApproval", req.WorkflowStatus); // business decision untouched
        var eaTask = await f.Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == created.EaTaskId);
        Assert.Equal(EaTaskExecutionStatus.InProgress, eaTask.ExecutionStatus); // business decision untouched
    }

    [Fact]
    public async Task ReviewRework_ClosesReviewPhase_AndOpensReworkPhase_AtSameCycle()
    {
        var f = await NewAsync();
        var created = await f.Service.CreateAsync(new ApprovalRequest { RequestTitle = "Rework flow", CreatedBy = "creator" });
        await f.Lifecycle.StartActualAsync(created.Id);
        await f.Lifecycle.SubmitForReviewAsync(created.Id, new SubmitForReviewRequestDto());

        var reworked = await f.Lifecycle.RequestTaskReworkAsync(created.Id, new RequestTaskReworkRequestDto { ReworkRemark = "fix it" }, null);

        Assert.Equal(TaskReviewStatus.ReworkRequested, reworked.Status);
        var phases = await f.Db.ApprovalPhaseTats.Where(p => p.ApprovalRequestId == created.Id).OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(3, phases.Count); // Actual (closed), Review (closed), Rework (open)
        var reworkPhase = phases[2];
        Assert.Equal(DelegationTaskType.Rework, reworkPhase.TaskType);
        Assert.Equal(1, reworkPhase.ReviewCycleNumber);
        Assert.Null(reworkPhase.EndedAt);

        var req = await f.Db.ApprovalRequests.AsNoTracking().SingleAsync(a => a.Id == created.Id);
        Assert.Equal("PendingApproval", req.WorkflowStatus); // untouched, same as review/approve
    }

    // ============================================================
    // §5 — Pause / Resume gating
    // ============================================================

    [Fact]
    public async Task Resume_WithoutAnyPause_Throws409()
    {
        var f = await NewAsync();
        var created = await f.Service.CreateAsync(new ApprovalRequest { RequestTitle = "No pause", CreatedBy = "creator" });

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.ResumeAsync(created.Id));
    }

    [Fact]
    public async Task Pause_Twice_SecondCallThrows409()
    {
        var f = await NewAsync();
        var created = await f.Service.CreateAsync(new ApprovalRequest { RequestTitle = "Double pause", CreatedBy = "creator" });
        await f.Lifecycle.StartActualAsync(created.Id);

        var paused = await f.Lifecycle.PauseAsync(created.Id, null);
        Assert.True(paused.IsPaused);

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.PauseAsync(created.Id, null));
    }

    [Fact]
    public async Task PauseThenResume_ReturnsApprovalDetailDto_WithIsPausedFlag()
    {
        var f = await NewAsync();
        var created = await f.Service.CreateAsync(new ApprovalRequest { RequestTitle = "Pause/resume", CreatedBy = "creator" });
        await f.Lifecycle.StartActualAsync(created.Id);

        var paused = await f.Lifecycle.PauseAsync(created.Id, new ApprovalPauseRequestDto { PauseReason = "lunch" });
        Assert.True(paused.IsPaused);
        Assert.Equal(created.Id, paused.ApprovalRequestId);

        var resumed = await f.Lifecycle.ResumeAsync(created.Id);
        Assert.False(resumed.IsPaused);
    }

    [Fact]
    public async Task Pause_AfterBusinessDecision_Throws409_NotInProgress()
    {
        var f = await NewAsync();
        var created = await f.Service.CreateAsync(new ApprovalRequest { RequestTitle = "Decided", CreatedBy = "creator" });
        await f.Lifecycle.ApproveAsync(created.Id, new ApprovalDecisionDto { Comment = "ok" });

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.PauseAsync(created.Id, null));
    }

    // ============================================================
    // §7 — Business Approve/Reject freezes the open phase and closes the pause anchor
    // ============================================================

    [Fact]
    public async Task Approve_FreezesOpenPhase_AndClosesPauseAnchor_WhenOneExists()
    {
        var f = await NewAsync();
        var created = await f.Service.CreateAsync(new ApprovalRequest { RequestTitle = "Freeze on approve", CreatedBy = "creator" });
        await f.Lifecycle.StartActualAsync(created.Id);

        await f.Lifecycle.PauseAsync(created.Id, null);
        await f.Lifecycle.ResumeAsync(created.Id);

        await f.Lifecycle.ApproveAsync(created.Id, new ApprovalDecisionDto { Comment = "ok", EmployeeName = "Approver" });

        var phase = Assert.Single(await f.Db.ApprovalPhaseTats.Where(p => p.ApprovalRequestId == created.Id).ToListAsync());
        Assert.NotNull(phase.EndedAt);

        var eaTask = await f.Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == created.EaTaskId);
        Assert.NotNull(eaTask.WorkflowInstanceId);
        var anchor = await f.Db.WorkflowInstances.AsNoTracking().SingleAsync(w => w.Id == eaTask.WorkflowInstanceId!.Value);
        Assert.False(anchor.IsActive);
        Assert.NotNull(anchor.CompletedAt);
    }

    [Fact]
    public async Task Reject_FreezesOpenPhase_EvenWithoutAPauseAnchor()
    {
        var f = await NewAsync();
        var created = await f.Service.CreateAsync(new ApprovalRequest { RequestTitle = "Freeze on reject", CreatedBy = "creator" });

        await f.Lifecycle.RejectAsync(created.Id, new ApprovalDecisionDto { Comment = "no budget", EmployeeName = "Approver" });

        var phase = Assert.Single(await f.Db.ApprovalPhaseTats.Where(p => p.ApprovalRequestId == created.Id).ToListAsync());
        Assert.NotNull(phase.EndedAt);
    }

    [Fact]
    public async Task RequestChanges_DoesNotFreezeTheOpenPhase()
    {
        var f = await NewAsync();
        var created = await f.Service.CreateAsync(new ApprovalRequest { RequestTitle = "Not frozen", CreatedBy = "creator" });

        await f.Lifecycle.RequestChangesAsync(created.Id, new ApprovalDecisionDto { Comment = "more detail", EmployeeName = "Approver" });

        var phase = Assert.Single(await f.Db.ApprovalPhaseTats.Where(p => p.ApprovalRequestId == created.Id).ToListAsync());
        Assert.Null(phase.EndedAt);
    }

    // ============================================================
    // §2 — GetApplicableForApprovalPhaseAsync: exact match then taskType-agnostic fallback
    // ============================================================

    [Fact]
    public async Task GetApplicableForApprovalPhaseAsync_PrefersExactTypeAndTaskTypeMatch()
    {
        var f = await NewAsync();
        var exact = await AddRuleAsync(f.Db, f.ModuleId, "Verification", null, DelegationTaskType.Actual, 90);
        await AddRuleAsync(f.Db, f.ModuleId, "Verification", null, null, 60); // type-only, taskType-agnostic fallback candidate
        var tatRules = new TatRuleRepository(f.Db);

        var match = await tatRules.GetApplicableForApprovalPhaseAsync(f.ModuleId, "Verification", null, DelegationTaskType.Actual, default);

        Assert.Single(match);
        Assert.Equal(exact.Id, match[0].Id);
        Assert.Equal(90, match[0].TatMinutes);
    }

    [Fact]
    public async Task GetApplicableForApprovalPhaseAsync_FallsBackToTaskTypeAgnosticCascade_WhenNoExactMatch()
    {
        var f = await NewAsync();
        // Only a TaskType=Actual rule exists — a Review-phase lookup has no exact match and must fall
        // back to the ordinary (taskType-agnostic) Type cascade, which only matches TaskType==null rows.
        await AddRuleAsync(f.Db, f.ModuleId, "Verification", null, DelegationTaskType.Actual, 90);
        var fallback = await AddRuleAsync(f.Db, f.ModuleId, "Verification", null, null, 60);
        var tatRules = new TatRuleRepository(f.Db);

        var match = await tatRules.GetApplicableForApprovalPhaseAsync(f.ModuleId, "Verification", null, DelegationTaskType.Review, default);

        Assert.Single(match);
        Assert.Equal(fallback.Id, match[0].Id);
        Assert.Equal(60, match[0].TatMinutes);
    }

    [Fact]
    public async Task GetApplicableForApprovalPhaseAsync_BlankType_SkipsExactMatch_GoesStraightToCascade()
    {
        var f = await NewAsync();
        var moduleOnly = await AddRuleAsync(f.Db, f.ModuleId, null, null, null, 45);
        var tatRules = new TatRuleRepository(f.Db);

        var match = await tatRules.GetApplicableForApprovalPhaseAsync(f.ModuleId, null, null, DelegationTaskType.Actual, default);

        Assert.Single(match);
        Assert.Equal(moduleOnly.Id, match[0].Id);
    }

}

/// <summary>
/// TatRuleService.SaveAsync's normalization (TatClassification.NormalizeAsync) uses raw relational SQL,
/// so — like DelegationTypeOnlyTatTests — these run against a real, throwaway Postgres database rather
/// than EF InMemory. Reuses the same ScratchTatDatabase fixture Delegation's own TAT tests use.
/// </summary>
public sealed class ApprovalTatRuleServiceIntegrationTests : IClassFixture<Jarvis5.Tests.EaFms.Delegation.ScratchTatDatabase>
{
    private readonly Jarvis5.Tests.EaFms.Delegation.ScratchTatDatabase _fx;
    private static readonly ICurrentUserService User = Mock.Of<ICurrentUserService>(u => u.UserName == "approval-tat-rule-actor" && u.UserId == 72L);

    public ApprovalTatRuleServiceIntegrationTests(Jarvis5.Tests.EaFms.Delegation.ScratchTatDatabase fx) => _fx = fx;

    private async Task<long> EnsureApprovalModuleIdAsync()
    {
        await using var db = _fx.Db();
        var existing = await db.BusinessModules.Where(m => m.Name == "EA Approval").Select(m => m.Id).SingleOrDefaultAsync();
        if (existing != 0) return existing;
        var module = new BusinessModule { Name = "EA Approval", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.BusinessModules.Add(module);
        await db.SaveChangesAsync();
        return module.Id;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    [Fact]
    public async Task TatRuleService_AcceptsApprovalRule_WithTaskTypeSet_AndSubtypeOmitted()
    {
        var moduleId = await EnsureApprovalModuleIdAsync();
        await using var db = _fx.Db();
        var ruleService = new TatRuleService(db, new TatRuleRepository(db), new Jarvis5.Validators.TatRuleDtoValidator(), User, Mock.Of<IAuditService>());

        var saved = await ruleService.SaveAsync(null, new SaveTatRuleDto
        {
            ModuleId = moduleId, Type = Unique("Verification"), TaskType = DelegationTaskType.Actual, TatMinutes = 90, IsActive = true
        }, default);

        Assert.Null(saved.Subtype);
        Assert.Equal(DelegationTaskType.Actual, saved.TaskType);
    }

    [Fact]
    public async Task TatRuleService_RejectsApprovalRule_WithBothSubtypeAndTaskType()
    {
        var moduleId = await EnsureApprovalModuleIdAsync();
        await using var db = _fx.Db();
        var ruleService = new TatRuleService(db, new TatRuleRepository(db), new Jarvis5.Validators.TatRuleDtoValidator(), User, Mock.Of<IAuditService>());

        await Assert.ThrowsAsync<BadRequestException>(() => ruleService.SaveAsync(null, new SaveTatRuleDto
        {
            ModuleId = moduleId, Type = Unique("Verification"), Subtype = "Finance", TaskType = DelegationTaskType.Actual, TatMinutes = 90, IsActive = true
        }, default));
    }

    [Fact]
    public async Task TatRuleService_StillAcceptsApprovalRule_WithSubtypeOnly_NoTaskType()
    {
        var moduleId = await EnsureApprovalModuleIdAsync();
        await using var db = _fx.Db();
        var ruleService = new TatRuleService(db, new TatRuleRepository(db), new Jarvis5.Validators.TatRuleDtoValidator(), User, Mock.Of<IAuditService>());

        var saved = await ruleService.SaveAsync(null, new SaveTatRuleDto
        {
            ModuleId = moduleId, Type = Unique("Verification"), Subtype = "Finance", TatMinutes = 90, IsActive = true
        }, default);

        Assert.Equal("Finance", saved.Subtype);
        Assert.Null(saved.TaskType);
    }
}
