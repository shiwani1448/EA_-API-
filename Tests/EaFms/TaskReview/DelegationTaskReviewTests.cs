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
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.TaskReview;

/// <summary>
/// Task Review/Rework (Phase 1) integration through Delegation: DelegationsController's new
/// routes resolve Delegation.EaTaskId and delegate to the one shared ITaskReviewService — no
/// review-cycle state-machine logic is duplicated in DelegationService itself. Delegation is,
/// however, the one module where the review outcome is NOT purely a side annotation: CompleteAsync
/// now opens the review cycle instead of finishing the Delegation, and ApproveReviewAsync is what
/// actually finalizes it (see FinalizeCompletionAsync) — so a completed-but-unapproved Delegation
/// can be sent back for rework without any separate "reopen" capability, and Approve only ever
/// needs to move a Delegation forward, never backward.
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
        var svc = new DelegationService(db, user, audit, numbers.Object, tasks.Object, env, taskReview, new TatRuleRepository(db));

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
        Assert.Equal(EaTaskExecutionStatus.NotStarted, submitted.ExecutionStatus);

        var reworked = await f.Svc.RequestReworkAsync(f.DelegationId, new RequestTaskReworkRequestDto { ReviewedById = "rev-1", ReviewedByName = "Reviewer One", ReworkRemark = "Fix the numbers" }, null);
        Assert.Equal(TaskReviewStatus.ReworkRequested, reworked.ReviewSummary.Status);
        Assert.Equal(1, reworked.ReviewSummary.ReviewCycleNumber);
        Assert.Equal("Fix the numbers", reworked.ReviewSummary.ReworkRemark);
        Assert.Equal(EaTaskExecutionStatus.NotStarted, reworked.ExecutionStatus);

        var resubmitted = await f.Svc.SubmitForReviewAsync(f.DelegationId, new SubmitForReviewRequestDto { ReviewerId = "rev-1", ReviewerName = "Reviewer One", SubmittedById = "doer-1", SubmittedByName = "Doer One" });
        Assert.Equal(TaskReviewStatus.PendingReview, resubmitted.ReviewSummary.Status);
        Assert.Equal(2, resubmitted.ReviewSummary.ReviewCycleNumber);

        var approved = await f.Svc.ApproveReviewAsync(f.DelegationId, new ApproveTaskReviewRequestDto { ReviewedById = "rev-1", ReviewedByName = "Reviewer One", ReviewRemark = "Looks good" }, null);
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

        // Unlike the shared engine's own Approve, ApproveReviewAsync (called above) finalizes the
        // Delegation as Completed — this delegation has no TAT configured, so TatUsedMinutes stays null.
        var task = await f.Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == f.EaTaskId);
        Assert.Equal(EaTaskExecutionStatus.Completed, task.ExecutionStatus);
        Assert.NotNull(task.CompletedAt);
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
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.ApproveReviewAsync(f.DelegationId, new ApproveTaskReviewRequestDto(), null));
    }

    [Fact]
    public async Task Rework_WithoutPendingReview_Rejected()
    {
        var f = await NewAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.RequestReworkAsync(f.DelegationId, new RequestTaskReworkRequestDto(), null));
    }

    [Fact]
    public async Task Approve_Twice_SecondCallRejected()
    {
        var f = await NewAsync();
        await f.Svc.SubmitForReviewAsync(f.DelegationId, new SubmitForReviewRequestDto());
        await f.Svc.ApproveReviewAsync(f.DelegationId, new ApproveTaskReviewRequestDto(), null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.ApproveReviewAsync(f.DelegationId, new ApproveTaskReviewRequestDto(), null));
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
    /// <summary>
    /// TAT keeps ticking through a pending review exactly as Phase 1 promises (review time counts
    /// toward existing TAT — no carve-out). What changed is Complete itself: it can no longer be
    /// called a second time while a review is already pending, because for Delegation Complete IS
    /// submit-for-review now (see CompleteAsync's own doc comment) — calling it again would be
    /// resubmitting the same cycle, which the shared engine correctly rejects.
    /// </summary>
    [Fact]
    public async Task PendingReview_TatKeepsRunning_AndCompleteIsBlockedUntilResolved()
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

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.CompleteAsync(f.DelegationId, null));
    }

    /// <summary>The real doer/assignee flow: Complete opens a review cycle (Delegation stays
    /// InProgress), and only Approve actually finalizes it — freezing TAT and marking Completed.</summary>
    [Fact]
    public async Task Complete_OpensReview_ApproveFinalizesCompletion()
    {
        var f = await NewAsync();
        var task = await f.Db.Tasks.SingleAsync(t => t.Id == f.EaTaskId);
        task.AllottedTatMinutes = 60;
        await f.Db.SaveChangesAsync();

        var submitted = await f.Svc.CompleteAsync(f.DelegationId, null);
        Assert.Equal(DelegationStatus.InProgress, submitted.Status);
        Assert.Equal(EaTaskExecutionStatus.NotStarted, submitted.ExecutionStatus);
        Assert.Equal(TaskReviewStatus.PendingReview, submitted.ReviewSummary.Status);
        Assert.Null(submitted.CompletedAt);

        var approved = await f.Svc.ApproveReviewAsync(f.DelegationId, new ApproveTaskReviewRequestDto { ReviewedById = "assignee-1", ReviewedByName = "The Assignee", ReviewRemark = "Nice work" }, null);
        Assert.Equal(DelegationStatus.Completed, approved.Status);
        Assert.Equal(EaTaskExecutionStatus.Completed, approved.ExecutionStatus);
        Assert.Equal(TaskReviewStatus.Approved, approved.ReviewSummary.Status);
        Assert.NotNull(approved.CompletedAt);
        Assert.NotNull(approved.TatUsedMinutes);

        var task2 = await f.Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == f.EaTaskId);
        Assert.NotNull(task2.TatUsedMinutes);
    }

    /// <summary>Rework leaves the Delegation exactly as Complete left it (InProgress, uncompleted) —
    /// there is nothing to reopen. The doer calls Complete again to open the next review cycle.</summary>
    [Fact]
    public async Task Complete_ThenRework_DelegationStaysInProgress_CompleteAgainOpensNextCycle()
    {
        var f = await NewAsync();

        var submitted = await f.Svc.CompleteAsync(f.DelegationId, null);
        Assert.Equal(1, submitted.ReviewSummary.ReviewCycleNumber);

        var reworked = await f.Svc.RequestReworkAsync(f.DelegationId, new RequestTaskReworkRequestDto { ReworkRemark = "Please redo section 2" }, null);
        Assert.Equal(DelegationStatus.InProgress, reworked.Status);
        Assert.Equal(EaTaskExecutionStatus.NotStarted, reworked.ExecutionStatus);
        Assert.Equal(TaskReviewStatus.ReworkRequested, reworked.ReviewSummary.Status);

        // Doer redoes the work and completes again — same InProgress Delegation, next review cycle.
        var resubmitted = await f.Svc.CompleteAsync(f.DelegationId, null);
        Assert.Equal(TaskReviewStatus.PendingReview, resubmitted.ReviewSummary.Status);
        Assert.Equal(2, resubmitted.ReviewSummary.ReviewCycleNumber);

        var approved = await f.Svc.ApproveReviewAsync(f.DelegationId, new ApproveTaskReviewRequestDto(), null);
        Assert.Equal(DelegationStatus.Completed, approved.Status);
        Assert.Equal(2, approved.ReviewSummary.ReviewCycleNumber);
    }

    /// <summary>The assignee's optional attachment on Approve is stored, surfaced on the current cycle's
    /// reviewSummary, and remains visible on that cycle's history row afterwards.</summary>
    [Fact]
    public async Task ApproveReview_WithAttachment_IsStoredAndSurfacedOnReviewSummaryAndHistory()
    {
        var f = await NewAsync();
        await f.Svc.CompleteAsync(f.DelegationId, null);

        var approved = await f.Svc.ApproveReviewAsync(f.DelegationId, new ApproveTaskReviewRequestDto(), Pdf("signoff.pdf"));
        Assert.NotNull(approved.ReviewSummary.AttachmentId);

        var history = await f.Svc.GetReviewHistoryAsync(f.DelegationId);
        Assert.Equal(approved.ReviewSummary.AttachmentId, Assert.Single(history).AttachmentId);

        var attachment = await f.Db.Attachments.AsNoTracking().SingleAsync(a => a.Id == approved.ReviewSummary.AttachmentId);
        Assert.Equal(("Delegation", "Delegation", f.DelegationId.ToString()), (attachment.RelatedModule, attachment.RelatedEntity, attachment.RelatedEntityId));
        Assert.Contains("DelegationReviewAttachment", attachment.Metadata);
    }

    /// <summary>Same as above for Rework — the attachment is tied to the cycle that got reworked, not the next one.</summary>
    [Fact]
    public async Task RequestRework_WithAttachment_IsStoredAndSurfacedOnReviewSummaryAndHistory_TiedToTheReworkedCycle()
    {
        var f = await NewAsync();
        await f.Svc.CompleteAsync(f.DelegationId, null);

        var reworked = await f.Svc.RequestReworkAsync(f.DelegationId, new RequestTaskReworkRequestDto(), Pdf("feedback.pdf"));
        Assert.NotNull(reworked.ReviewSummary.AttachmentId);
        Assert.Equal(1, reworked.ReviewSummary.ReviewCycleNumber);

        // Cycle 2 (no attachment yet) must not pick up cycle 1's attachment.
        var resubmitted = await f.Svc.CompleteAsync(f.DelegationId, null);
        Assert.Equal(2, resubmitted.ReviewSummary.ReviewCycleNumber);
        Assert.Null(resubmitted.ReviewSummary.AttachmentId);

        var history = await f.Svc.GetReviewHistoryAsync(f.DelegationId);
        Assert.Equal(reworked.ReviewSummary.AttachmentId, history.Single(h => h.ReviewCycleNumber == 1).AttachmentId);
        Assert.Null(history.Single(h => h.ReviewCycleNumber == 2).AttachmentId);
    }

    private static IFormFile Pdf(string name)
    {
        var builder = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
        builder.AddPage(200, 200);
        var ms = new MemoryStream(builder.Build());
        return new FormFile(ms, 0, ms.Length, "attachment", name) { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
    }
}
