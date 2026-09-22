using System;
using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.TaskReview;

/// <summary>
/// Task Review/Rework (Phase 1) integration through Approval Management: ApprovalsLifecycleController's
/// new routes resolve ApprovalRequest.EaTaskId and delegate to the one shared ITaskReviewService.
/// Proves this is entirely independent of Approval's own business WorkflowStatus/ApprovalCycle
/// lifecycle (Submit/Approve/Reject/RequestChanges/Resubmit) — PendingApproval/Approved/Rejected/
/// ChangesRequested are never reused for review state, and ApprovalCycle rows are never touched.
/// </summary>
public class ApprovalTaskReviewTests
{
    private sealed class Fx
    {
        public required EaFmsDbContext Db { get; init; }
        public required ApprovalLifecycleService Lifecycle { get; init; }
        public required long ApprovalRequestId { get; init; }
        public required long EaTaskId { get; init; }
    }

    private async Task<Fx> NewAsync()
    {
        var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
        var module = new BusinessModule { Name = "EA Approval", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.BusinessModules.Add(module);
        await db.SaveChangesAsync();

        var numbers = new Mock<IApprovalNumberRepository>();
        var n = 0;
        numbers.Setup(r => r.GenerateNextReferenceNoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => $"APR-REV-{++n:D4}");
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
        var user = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "apr-review-actor" && u.UserId == 31L);
        var taskReview = new TaskReviewService(db, new TaskReviewRepository(db), user, audit);
        var service = new ApprovalService(db, audit, numbers.Object, tasks.Object);
        var lifecycle = new ApprovalLifecycleService(db, audit, taskReview);

        var created = await service.CreateAsync(new ApprovalRequest { RequestTitle = "Review-cycle approval", CreatedBy = "creator", ApproverId = "approver-1" });
        return new Fx { Db = db, Lifecycle = lifecycle, ApprovalRequestId = created.Id, EaTaskId = created.EaTaskId };
    }

    [Fact]
    public async Task FullCycle_Submit_Rework_Resubmit_Approve()
    {
        var f = await NewAsync();

        var submitted = await f.Lifecycle.SubmitForReviewAsync(f.ApprovalRequestId, new SubmitForReviewRequestDto { ReviewerId = "rev-5", ReviewerName = "QA Reviewer" });
        Assert.Equal(TaskReviewStatus.PendingReview, submitted.Status);
        Assert.Equal(1, submitted.ReviewCycleNumber);

        var reworked = await f.Lifecycle.RequestTaskReworkAsync(f.ApprovalRequestId, new RequestTaskReworkRequestDto { ReworkRemark = "Add justification" });
        Assert.Equal(TaskReviewStatus.ReworkRequested, reworked.Status);

        var resubmitted = await f.Lifecycle.SubmitForReviewAsync(f.ApprovalRequestId, new SubmitForReviewRequestDto { ReviewerId = "rev-5", ReviewerName = "QA Reviewer" });
        Assert.Equal(2, resubmitted.ReviewCycleNumber);

        var approved = await f.Lifecycle.ApproveReviewAsync(f.ApprovalRequestId, new ApproveTaskReviewRequestDto { ReviewRemark = "Fine" });
        Assert.Equal(TaskReviewStatus.Approved, approved.Status);
        Assert.Equal(2, approved.ReviewCycleNumber);

        var history = await f.Lifecycle.GetReviewHistoryAsync(f.ApprovalRequestId);
        Assert.Equal(2, history.Count);
        Assert.Equal(TaskReviewStatus.ReworkRequested, history[0].Status);
        Assert.Equal(TaskReviewStatus.Approved, history[1].Status);

        // Approval's OWN business lifecycle (WorkflowStatus / ApprovalCycle) is completely
        // untouched by Task Review — Task Review never reuses PendingApproval/Approved/
        // Rejected/ChangesRequested. ApprovalService.CreateAsync already writes exactly one
        // ApprovalCycle row (cycle 1, "PendingApproval") at creation time — Task Review must
        // not add, remove or modify it.
        var request = await f.Db.ApprovalRequests.AsNoTracking().SingleAsync(a => a.Id == f.ApprovalRequestId);
        Assert.Equal("PendingApproval", request.WorkflowStatus);
        Assert.Null(request.ApprovedAt);
        Assert.Null(request.RejectedAt);
        var businessCycle = Assert.Single(await f.Db.ApprovalCycles.Where(c => c.ApprovalRequestId == f.ApprovalRequestId).ToListAsync());
        Assert.Equal(1, businessCycle.CycleNo);
        Assert.Equal("PendingApproval", businessCycle.Status);

        // EaTask.ExecutionStatus stays InProgress (Approval's own create-time behavior) — never
        // driven to Completed by review actions (only Approve/Reject on the business side do that).
        var task = await f.Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == f.EaTaskId);
        Assert.Equal(EaTaskExecutionStatus.InProgress, task.ExecutionStatus);
        Assert.Null(task.CompletedAt);

        // No WorkPause / WorkflowInstance was ever created — Approval never uses either.
        Assert.Empty(await f.Db.WorkPauses.ToListAsync());
        Assert.Empty(await f.Db.WorkflowInstances.ToListAsync());
    }

    [Fact]
    public async Task SubmitForReview_WhilePendingReview_Rejected()
    {
        var f = await NewAsync();
        await f.Lifecycle.SubmitForReviewAsync(f.ApprovalRequestId, new SubmitForReviewRequestDto());

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.SubmitForReviewAsync(f.ApprovalRequestId, new SubmitForReviewRequestDto()));
    }

    [Fact]
    public async Task Approve_WithoutPendingReview_Rejected()
    {
        var f = await NewAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.ApproveReviewAsync(f.ApprovalRequestId, new ApproveTaskReviewRequestDto()));
    }

    [Fact]
    public async Task Rework_WithoutPendingReview_Rejected()
    {
        var f = await NewAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.RequestTaskReworkAsync(f.ApprovalRequestId, new RequestTaskReworkRequestDto()));
    }

    [Fact]
    public async Task Approve_Twice_SecondCallRejected()
    {
        var f = await NewAsync();
        await f.Lifecycle.SubmitForReviewAsync(f.ApprovalRequestId, new SubmitForReviewRequestDto());
        await f.Lifecycle.ApproveReviewAsync(f.ApprovalRequestId, new ApproveTaskReviewRequestDto());

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Lifecycle.ApproveReviewAsync(f.ApprovalRequestId, new ApproveTaskReviewRequestDto()));
    }

    // ================================================================
    // Phase 10 — D: Business RequestChanges does NOT create or change TaskReview
    // ================================================================

    [Fact]
    public async Task Phase10D_BusinessRequestChanges_DoesNotCreateOrChangeTaskReview()
    {
        var f = await NewAsync();

        // RequestChanges is a business-lifecycle action only — no TaskReview row should appear
        await f.Lifecycle.RequestChangesAsync(f.ApprovalRequestId,
            new ApprovalDecisionDto { Comment = "Need more detail", EmployeeName = "Approver One" });

        // Business status changed
        var req = await f.Db.ApprovalRequests.AsNoTracking().SingleAsync(a => a.Id == f.ApprovalRequestId);
        Assert.Equal("ChangesRequested", req.WorkflowStatus);

        // Zero TaskReview rows — RequestChanges is entirely independent of TaskReview
        var reviewRows = await f.Db.Set<Jarvis5.Entities.EaFms.TaskReview>()
            .Where(r => r.EaTaskId == f.EaTaskId).ToListAsync();
        Assert.Empty(reviewRows);
    }

    // ================================================================
    // Phase 10 — E: Business Resubmit does NOT create or change TaskReview
    // ================================================================

    [Fact]
    public async Task Phase10E_BusinessResubmit_DoesNotCreateOrChangeTaskReview()
    {
        var f = await NewAsync();

        // First put in ChangesRequested via business lifecycle
        await f.Lifecycle.RequestChangesAsync(f.ApprovalRequestId,
            new ApprovalDecisionDto { Comment = "Need more detail", EmployeeName = "Approver One" });

        // Then Resubmit — also a pure business action
        await f.Lifecycle.ResubmitAsync(f.ApprovalRequestId);

        // Business status back to PendingApproval with a new cycle
        var req = await f.Db.ApprovalRequests.AsNoTracking().SingleAsync(a => a.Id == f.ApprovalRequestId);
        Assert.Equal("PendingApproval", req.WorkflowStatus);
        var cycles = await f.Db.ApprovalCycles
            .Where(c => c.ApprovalRequestId == f.ApprovalRequestId).ToListAsync();
        Assert.Equal(2, cycles.Count); // cycle 1 (ChangesRequested) + cycle 2 (PendingApproval)

        // Still zero TaskReview rows — Resubmit is entirely independent of TaskReview
        var reviewRows = await f.Db.Set<Jarvis5.Entities.EaFms.TaskReview>()
            .Where(r => r.EaTaskId == f.EaTaskId).ToListAsync();
        Assert.Empty(reviewRows);
    }

    // ================================================================
    // Phase 10 — F: Business Approve preserves existing business behavior
    //              (ApprovalCycle.Status=Approved, ApprovedAt set, EaTask=Completed)
    // ================================================================

    [Fact]
    public async Task Phase10F_BusinessApprove_PreservesExistingApprovalBehavior()
    {
        var f = await NewAsync();

        await f.Lifecycle.ApproveAsync(f.ApprovalRequestId,
            new ApprovalDecisionDto { EmployeeId = "EMP-001", EmployeeName = "Director Smith" });

        var req = await f.Db.ApprovalRequests.AsNoTracking().SingleAsync(a => a.Id == f.ApprovalRequestId);
        Assert.Equal("Approved", req.WorkflowStatus);
        Assert.NotNull(req.ApprovedAt);
        Assert.Null(req.RejectedAt);
        Assert.Contains("Director Smith", req.ApprovedBy ?? "");

        var cycle = await f.Db.ApprovalCycles.AsNoTracking()
            .SingleAsync(c => c.ApprovalRequestId == f.ApprovalRequestId);
        Assert.Equal("Approved", cycle.Status);

        var eaTask = await f.Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == f.EaTaskId);
        Assert.Equal(EaTaskExecutionStatus.Completed, eaTask.ExecutionStatus);
        Assert.NotNull(eaTask.CompletedAt);

        // Business Approve never creates a TaskReview row
        var reviewRows = await f.Db.Set<Jarvis5.Entities.EaFms.TaskReview>()
            .Where(r => r.EaTaskId == f.EaTaskId).ToListAsync();
        Assert.Empty(reviewRows);
    }

    // ================================================================
    // Phase 10 — G: Business Reject preserves existing business behavior
    //              (ApprovalCycle.Status=Rejected, RejectedAt set, EaTask=Completed)
    // ================================================================

    [Fact]
    public async Task Phase10G_BusinessReject_PreservesExistingRejectionBehavior()
    {
        var f = await NewAsync();

        await f.Lifecycle.RejectAsync(f.ApprovalRequestId,
            new ApprovalDecisionDto { Comment = "Budget exceeded", EmployeeName = "Director Jones" });

        var req = await f.Db.ApprovalRequests.AsNoTracking().SingleAsync(a => a.Id == f.ApprovalRequestId);
        Assert.Equal("Rejected", req.WorkflowStatus);
        Assert.NotNull(req.RejectedAt);
        Assert.Null(req.ApprovedAt);
        Assert.Contains("Director Jones", req.RejectedBy ?? "");

        var cycle = await f.Db.ApprovalCycles.AsNoTracking()
            .SingleAsync(c => c.ApprovalRequestId == f.ApprovalRequestId);
        Assert.Equal("Rejected", cycle.Status);

        var eaTask = await f.Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == f.EaTaskId);
        Assert.Equal(EaTaskExecutionStatus.Completed, eaTask.ExecutionStatus);
        Assert.NotNull(eaTask.CompletedAt);

        // Business Reject never creates a TaskReview row
        var reviewRows = await f.Db.Set<Jarvis5.Entities.EaFms.TaskReview>()
            .Where(r => r.EaTaskId == f.EaTaskId).ToListAsync();
        Assert.Empty(reviewRows);
    }

    // ================================================================
    // Phase 10 — H: TaskReview history and ApprovalCycle history are independent —
    //              each shows only its own type of events, neither cross-contaminates.
    // ================================================================

    [Fact]
    public async Task Phase10H_TaskReviewHistory_And_ApprovalCycleHistory_AreIndependent()
    {
        var f = await NewAsync();

        // Perform two business-lifecycle operations that add ApprovalCycle rows
        await f.Lifecycle.RequestChangesAsync(f.ApprovalRequestId,
            new ApprovalDecisionDto { Comment = "Round 1 changes", EmployeeName = "Approver" });
        await f.Lifecycle.ResubmitAsync(f.ApprovalRequestId);

        // Perform two TaskReview operations that add TaskReview rows
        await f.Lifecycle.SubmitForReviewAsync(f.ApprovalRequestId,
            new SubmitForReviewRequestDto { ReviewerId = "rev-1", ReviewerName = "Quality Lead" });
        await f.Lifecycle.RequestTaskReworkAsync(f.ApprovalRequestId,
            new RequestTaskReworkRequestDto { ReworkRemark = "Fix formatting" });

        // ApprovalCycle history: exactly 2 cycles (Round 1 + Resubmit)
        var cycles = await f.Db.ApprovalCycles
            .Where(c => c.ApprovalRequestId == f.ApprovalRequestId)
            .OrderBy(c => c.CycleNo)
            .ToListAsync();
        Assert.Equal(2, cycles.Count);
        Assert.Equal("ChangesRequested", cycles[0].Status);
        Assert.Equal("PendingApproval", cycles[1].Status);

        // TaskReview history: exactly 1 row (cycle 1, ReworkRequested)
        var reviewHistory = await f.Lifecycle.GetReviewHistoryAsync(f.ApprovalRequestId);
        Assert.Single(reviewHistory);
        Assert.Equal(TaskReviewStatus.ReworkRequested, reviewHistory[0].Status);
        Assert.Equal(1, reviewHistory[0].ReviewCycleNumber);

        // Neither contaminates the other:
        // ApprovalCycles have no ReviewStatus; TaskReview rows have no ApprovalCycleNo field
        Assert.DoesNotContain(cycles, c => c.Status is "PendingReview" or "Approved" or "ReworkRequested");
        Assert.DoesNotContain(reviewHistory, r => r.Status is "PendingApproval" or "ChangesRequested" or "Rejected");
    }

    // ================================================================
    // Phase 11 — Eligibility cross-check:
    //   Business Approve/Reject completes the EaTask → a subsequent SubmitForReview
    //   is blocked with "task is already Completed" (current behavior, not a redesign).
    //   This is documented behavior, not a bug: the business decision closes the task.
    // ================================================================

    [Fact]
    public async Task Phase11_BusinessApprove_CompletesEaTask_ThenTaskReviewSubmit_IsBlocked()
    {
        var f = await NewAsync();

        // Business Approve completes the underlying EaTask
        await f.Lifecycle.ApproveAsync(f.ApprovalRequestId,
            new ApprovalDecisionDto { EmployeeId = "EMP-002", EmployeeName = "Senior Director" });

        var eaTask = await f.Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == f.EaTaskId);
        Assert.Equal(EaTaskExecutionStatus.Completed, eaTask.ExecutionStatus);

        // Attempting TaskReview Submit after business Approve is rejected by the engine
        // because EaTask.ExecutionStatus == Completed.
        // This is the current documented behavior: the business decision closes the task,
        // and a Completed task cannot be submitted for review.
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => f.Lifecycle.SubmitForReviewAsync(f.ApprovalRequestId,
                new SubmitForReviewRequestDto { ReviewerId = "rev-late", ReviewerName = "Late Reviewer" }));

        Assert.Contains("Completed", ex.Message);

        // Zero TaskReview rows — the submit was rejected before any write
        Assert.Empty(await f.Db.Set<Jarvis5.Entities.EaFms.TaskReview>()
            .Where(r => r.EaTaskId == f.EaTaskId).ToListAsync());
    }

    [Fact]
    public async Task Phase11_BusinessReject_CompletesEaTask_ThenTaskReviewSubmit_IsBlocked()
    {
        var f = await NewAsync();

        await f.Lifecycle.RejectAsync(f.ApprovalRequestId,
            new ApprovalDecisionDto { Comment = "Not viable", EmployeeName = "Finance Director" });

        var eaTask = await f.Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == f.EaTaskId);
        Assert.Equal(EaTaskExecutionStatus.Completed, eaTask.ExecutionStatus);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => f.Lifecycle.SubmitForReviewAsync(f.ApprovalRequestId,
                new SubmitForReviewRequestDto { ReviewerId = "rev-late", ReviewerName = "Late Reviewer" }));

        Assert.Contains("Completed", ex.Message);

        Assert.Empty(await f.Db.Set<Jarvis5.Entities.EaFms.TaskReview>()
            .Where(r => r.EaTaskId == f.EaTaskId).ToListAsync());
    }

    // ================================================================
    // Phase 11 — Coexistence: TaskReview submitted BEFORE business Approve.
    //   The PendingReview cycle remains readable in history after business Approve
    //   (historical rows are never deleted). The business Approve does not close/
    //   modify the TaskReview row — they remain fully independent.
    // ================================================================

    [Fact]
    public async Task Phase11_TaskReviewPending_ThenBusinessApprove_BothCyclesCoexistIndependently()
    {
        var f = await NewAsync();

        // Submit for Task Review first (cycle 1, PendingReview)
        await f.Lifecycle.SubmitForReviewAsync(f.ApprovalRequestId,
            new SubmitForReviewRequestDto { ReviewerId = "rev-qa", ReviewerName = "QA Lead" });

        // Business Approve fires independently — it does NOT alter the TaskReview row
        await f.Lifecycle.ApproveAsync(f.ApprovalRequestId,
            new ApprovalDecisionDto { EmployeeId = "EMP-003", EmployeeName = "Director Kim" });

        // Business state: Approved
        var req = await f.Db.ApprovalRequests.AsNoTracking().SingleAsync(a => a.Id == f.ApprovalRequestId);
        Assert.Equal("Approved", req.WorkflowStatus);

        // TaskReview row: still PendingReview — business Approve did not touch it
        var reviewRows = await f.Db.Set<Jarvis5.Entities.EaFms.TaskReview>()
            .Where(r => r.EaTaskId == f.EaTaskId).ToListAsync();
        Assert.Single(reviewRows);
        Assert.Equal(TaskReviewStatus.PendingReview, reviewRows[0].ReviewStatus);

        // EaTask: Completed by business Approve
        var eaTask = await f.Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == f.EaTaskId);
        Assert.Equal(EaTaskExecutionStatus.Completed, eaTask.ExecutionStatus);

        // Now a second SubmitForReview is blocked (task Completed)
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => f.Lifecycle.SubmitForReviewAsync(f.ApprovalRequestId, new SubmitForReviewRequestDto()));
    }
}
