using System;
using System.IO;
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
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.TaskReview;

/// <summary>
/// Task Review/Rework (Phase 1) integration through Delegation: DelegationsController's new
/// routes resolve Delegation.EaTaskId and delegate to the one shared ITaskReviewService — no
/// state-machine logic is duplicated in DelegationService itself.
/// </summary>
public class DelegationTaskReviewTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    public void Dispose() { try { Directory.Delete(_root, true); } catch { /* best-effort */ } }

    private sealed class Fx
    {
        public required EaFmsDbContext Db { get; init; }
        public required DelegationService Svc { get; init; }
        public required long DelegationId { get; init; }
        public required long EaTaskId { get; init; }
    }

    private async Task<Fx> NewAsync()
    {
        var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
        var module = new BusinessModule { Name = DelegationService.DelegationBusinessModuleName, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.BusinessModules.Add(module);
        db.Statuses.Add(new Status { Name = "In Progress", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "ea-actor" && u.UserId == 42L);
        var numbers = new Mock<IDelegationNumberRepository>();
        var seq = 0;
        numbers.Setup(r => r.GenerateNextReferenceNoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => $"DLG-REV-{++seq:D6}");
        var tasks = new Mock<IEaTaskService>();
        tasks.Setup(s => s.CreateWithoutTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateEaTaskDto dto, CancellationToken _) =>
            {
                var t = new EaTask { BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = dto.BusinessRecordId,
                    Task = dto.Task ?? dto.BusinessRecordId, ExecutionStatus = EaTaskExecutionStatus.NotStarted, IsActive = true, CreatedBy = "ea-actor", CreatedDate = DateTime.UtcNow };
                db.Tasks.Add(t); db.SaveChanges();
                return new EaTaskResponseDto { EaTaskId = t.Id, ModuleId = module.Id, ModuleName = t.ModuleName, BusinessRecordId = t.BusinessRecordId,
                    Task = t.Task, ExecutionStatus = t.ExecutionStatus, IsActive = true, CreatedBy = t.CreatedBy, CreatedDate = t.CreatedDate };
            });
        var audit = Mock.Of<IAuditService>();
        var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == _root);
        var taskReview = new TaskReviewService(db, new TaskReviewRepository(db), user, audit);
        var svc = new DelegationService(db, user, audit, numbers.Object, tasks.Object, env, taskReview);

        var created = await svc.CreateAsync(new DelegationCreateRequestDto { Title = "Review me", DoerId = "emp-1", EndDate = DateTime.UtcNow.AddDays(5) });
        await svc.StartAsync(created.DelegationId);

        return new Fx { Db = db, Svc = svc, DelegationId = created.DelegationId, EaTaskId = created.EaTaskId };
    }

    [Fact]
    public async Task FullCycle_Submit_Rework_Resubmit_Approve()
    {
        var f = await NewAsync();

        var submitted = await f.Svc.SubmitForReviewAsync(f.DelegationId, new SubmitForReviewRequestDto { ReviewerId = "rev-1", ReviewerName = "Reviewer One", SubmittedById = "doer-1", SubmittedByName = "Doer One" });
        Assert.Equal(TaskReviewStatus.PendingReview, submitted.ReviewSummary.Status);
        Assert.Equal(1, submitted.ReviewSummary.ReviewCycleNumber);
        Assert.Equal("rev-1", submitted.ReviewSummary.ReviewerId);
        Assert.Equal(EaTaskExecutionStatus.InProgress, submitted.ExecutionStatus);

        var reworked = await f.Svc.RequestReworkAsync(f.DelegationId, new RequestTaskReworkRequestDto { ReviewedById = "rev-1", ReviewedByName = "Reviewer One", ReworkRemark = "Fix the numbers" });
        Assert.Equal(TaskReviewStatus.ReworkRequested, reworked.ReviewSummary.Status);
        Assert.Equal(1, reworked.ReviewSummary.ReviewCycleNumber);
        Assert.Equal("Fix the numbers", reworked.ReviewSummary.ReworkRemark);
        Assert.Equal(EaTaskExecutionStatus.InProgress, reworked.ExecutionStatus);

        var resubmitted = await f.Svc.SubmitForReviewAsync(f.DelegationId, new SubmitForReviewRequestDto { ReviewerId = "rev-1", ReviewerName = "Reviewer One", SubmittedById = "doer-1", SubmittedByName = "Doer One" });
        Assert.Equal(TaskReviewStatus.PendingReview, resubmitted.ReviewSummary.Status);
        Assert.Equal(2, resubmitted.ReviewSummary.ReviewCycleNumber);

        var approved = await f.Svc.ApproveReviewAsync(f.DelegationId, new ApproveTaskReviewRequestDto { ReviewedById = "rev-1", ReviewedByName = "Reviewer One", ReviewRemark = "Looks good" });
        Assert.Equal(TaskReviewStatus.Approved, approved.ReviewSummary.Status);
        Assert.Equal(2, approved.ReviewSummary.ReviewCycleNumber);
        Assert.Equal("Looks good", approved.ReviewSummary.ReviewRemark);

        // Cycle history preserves both cycles — never overwritten.
        var history = await f.Svc.GetReviewHistoryAsync(f.DelegationId);
        Assert.Equal(2, history.Count);
        Assert.Equal((1, TaskReviewStatus.ReworkRequested), (history[0].ReviewCycleNumber, history[0].Status));
        Assert.Equal((2, TaskReviewStatus.Approved), (history[1].ReviewCycleNumber, history[1].Status));
        Assert.Equal("Fix the numbers", history[0].ReworkRemark);
        Assert.Equal("Looks good", history[1].ReviewRemark);

        // ExecutionStatus/TAT were never touched by any review action; module Complete stays independent (Phase 1).
        var task = await f.Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == f.EaTaskId);
        Assert.Equal(EaTaskExecutionStatus.InProgress, task.ExecutionStatus);
        Assert.Null(task.CompletedAt);
        Assert.Null(task.TatUsedMinutes);

        // No WorkPause was ever created by review actions.
        Assert.Empty(await f.Db.WorkPauses.ToListAsync());
    }

    [Fact]
    public async Task SubmitForReview_WhilePendingReview_Rejected()
    {
        var f = await NewAsync();
        await f.Svc.SubmitForReviewAsync(f.DelegationId, new SubmitForReviewRequestDto());

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.SubmitForReviewAsync(f.DelegationId, new SubmitForReviewRequestDto()));
    }

    [Fact]
    public async Task Approve_WithoutPendingReview_Rejected()
    {
        var f = await NewAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.ApproveReviewAsync(f.DelegationId, new ApproveTaskReviewRequestDto()));
    }

    [Fact]
    public async Task Rework_WithoutPendingReview_Rejected()
    {
        var f = await NewAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.RequestReworkAsync(f.DelegationId, new RequestTaskReworkRequestDto()));
    }

    [Fact]
    public async Task Approve_Twice_SecondCallRejected()
    {
        var f = await NewAsync();
        await f.Svc.SubmitForReviewAsync(f.DelegationId, new SubmitForReviewRequestDto());
        await f.Svc.ApproveReviewAsync(f.DelegationId, new ApproveTaskReviewRequestDto());

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.ApproveReviewAsync(f.DelegationId, new ApproveTaskReviewRequestDto()));
    }

    [Fact]
    public async Task NoReviewYet_SummaryIsNullShaped_NotFakeZero()
    {
        var f = await NewAsync();
        var view = await f.Svc.GetByIdAsync(f.DelegationId);
        Assert.Null(view.ReviewSummary.Status);
        Assert.Equal(0, view.ReviewSummary.ReviewCycleNumber);
        Assert.Empty(await f.Svc.GetReviewHistoryAsync(f.DelegationId));
    }
    [Fact]
    public async Task PendingReview_TatKeepsRunning_AndCompleteRemainsIndependent()
    {
        var f = await NewAsync();
        var task = await f.Db.Tasks.SingleAsync(t => t.Id == f.EaTaskId);
        task.AllottedTatMinutes = 60;
        await f.Db.SaveChangesAsync();
        var submitted = await f.Svc.SubmitForReviewAsync(f.DelegationId, new());
        await Task.Delay(40);
        var pending = await f.Svc.GetByIdAsync(f.DelegationId);
        Assert.True(pending.TatSummary.Tat > submitted.TatSummary.Tat);
        Assert.Equal(TimeSpan.Zero, pending.TatSummary.PauseTime);
        var completed = await f.Svc.CompleteAsync(f.DelegationId, null);
        Assert.Equal(EaTaskExecutionStatus.Completed, completed.ExecutionStatus);
        Assert.Equal(TaskReviewStatus.PendingReview, completed.ReviewSummary.Status);
        Assert.Empty(await f.Db.WorkPauses.ToListAsync());
    }
}
