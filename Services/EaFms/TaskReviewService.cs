using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// Phase 1: review time counts toward existing task TAT (Option A). This service never
/// creates/closes a WorkPause, never touches EaTask.StartedAt/CompletedAt/TatUsedMinutes/
/// ExecutionStatus, and never calls a module's own Complete method — Approve only marks
/// the review cycle Approved; the module's existing Complete lifecycle remains independent
/// and unchanged in this phase.
/// </summary>
public class TaskReviewService : ITaskReviewService
{
    private readonly EaFmsDbContext _db;
    private readonly ITaskReviewRepository _repo;
    private readonly ICurrentUserService _user;
    private readonly IAuditService _audit;

    public TaskReviewService(EaFmsDbContext db, ITaskReviewRepository repo, ICurrentUserService user, IAuditService audit)
    {
        _db = db;
        _repo = repo;
        _user = user;
        _audit = audit;
    }

    private string Actor() => _user.UserName ?? _user.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private async Task<IDbContextTransaction?> BeginTxIfNeededAsync(CancellationToken ct) =>
        _db.Database.CurrentTransaction is null
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;

    /// <summary>Row-locks the EaTask (relational only) so concurrent submit/approve/rework calls on the
    /// same task serialize instead of both reading the same "current cycle" state and both succeeding.</summary>
    private async Task<EaTask> LockEaTaskAsync(long eaTaskId, CancellationToken ct)
    {
        EaTask? task;
        if (_db.Database.IsRelational())
        {
            var rows = await _db.Tasks.FromSqlInterpolated(
                $"SELECT * FROM public.ea_tasks WHERE \"Id\" = {eaTaskId} FOR UPDATE").AsNoTracking().ToListAsync(ct);
            task = rows.SingleOrDefault();
        }
        else
        {
            task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == eaTaskId, ct);
        }
        if (task is null || task.IsDeleted)
            throw new NotFoundException($"EaTask {eaTaskId} not found.");
        // Scope is checked on writes only. Historical reviews remain readable.
        var supported = await _db.BusinessModules.AnyAsync(m => m.Id == task.BusinessModuleId
            && (m.Name == "Delegation" || m.Name == "EA Approval"), ct);
        if (!supported)
            throw new BusinessRuleException("Task Review is supported only for Delegation and Approval Management.");
        return task;
    }

    public async Task<TaskReviewSummaryDto> SubmitForReviewAsync(long eaTaskId, SubmitForReviewRequestDto dto, CancellationToken ct = default)
    {
        await using var tx = await BeginTxIfNeededAsync(ct);
        try
        {
            var task = await LockEaTaskAsync(eaTaskId, ct);
            if (task.ExecutionStatus == EaTaskExecutionStatus.Cancelled)
                throw new BusinessRuleException("Cannot submit for review: task is Cancelled.");
            if (task.ExecutionStatus == EaTaskExecutionStatus.Completed)
                throw new BusinessRuleException("Cannot submit for review: task is already Completed.");

            var current = await _repo.GetCurrentAsync(eaTaskId, ct);
            if (current is not null && current.ReviewStatus == TaskReviewStatus.PendingReview)
                throw new BusinessRuleException("A review is already pending for this task.");

            var nextCycleNo = (current?.ReviewCycleNo ?? 0) + 1;
            var now = Clock.UtcNowTz;
            var actor = Actor();

            var review = new TaskReview
            {
                EaTaskId = eaTaskId,
                ReviewCycleNo = nextCycleNo,
                ReviewStatus = TaskReviewStatus.PendingReview,
                ReviewerId = dto.ReviewerId,
                ReviewerName = dto.ReviewerName,
                SubmittedById = dto.SubmittedById,
                SubmittedByName = dto.SubmittedByName,
                SubmittedAt = now,
                CreatedBy = actor,
                CreatedDate = now
            };
            await _repo.AddAsync(review, ct);

            _audit.AddAudit("TASK_REVIEW_SUBMIT", "TaskReview", nameof(TaskReview), eaTaskId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                null, new { EaTaskId = eaTaskId, review.ReviewCycleNo, review.ReviewerId, review.ReviewerName, review.SubmittedById, review.SubmittedByName },
                "Submitted for review");

            await _db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
            return ToSummary(review);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<TaskReviewSummaryDto> ApproveAsync(long eaTaskId, ApproveTaskReviewRequestDto dto, CancellationToken ct = default)
    {
        await using var tx = await BeginTxIfNeededAsync(ct);
        try
        {
            await LockEaTaskAsync(eaTaskId, ct);
            var current = await _repo.GetCurrentAsync(eaTaskId, ct)
                ?? throw new BusinessRuleException("Task has never been submitted for review.");
            if (current.ReviewStatus != TaskReviewStatus.PendingReview)
                throw new BusinessRuleException($"Cannot approve: current review cycle is '{current.ReviewStatus}', not PendingReview.");

            var now = Clock.UtcNowTz;
            var oldStatus = current.ReviewStatus;
            current.ReviewStatus = TaskReviewStatus.Approved;
            current.ReviewedAt = now;
            current.ReviewedById = dto.ReviewedById;
            current.ReviewedByName = dto.ReviewedByName;
            current.ReviewRemark = dto.ReviewRemark;
            current.ModifiedBy = Actor();
            current.ModifiedDate = now;
            _repo.Update(current);

            _audit.AddAudit("TASK_REVIEW_APPROVE", "TaskReview", nameof(TaskReview), eaTaskId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                new { ReviewStatus = oldStatus }, new { current.ReviewCycleNo, current.ReviewStatus, current.ReviewedById, current.ReviewedByName, current.ReviewRemark },
                "Review approved");

            await _db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
            return ToSummary(current);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<TaskReviewSummaryDto> RequestReworkAsync(long eaTaskId, RequestTaskReworkRequestDto dto, CancellationToken ct = default)
    {
        await using var tx = await BeginTxIfNeededAsync(ct);
        try
        {
            await LockEaTaskAsync(eaTaskId, ct);
            var current = await _repo.GetCurrentAsync(eaTaskId, ct)
                ?? throw new BusinessRuleException("Task has never been submitted for review.");
            if (current.ReviewStatus != TaskReviewStatus.PendingReview)
                throw new BusinessRuleException($"Cannot request rework: current review cycle is '{current.ReviewStatus}', not PendingReview.");

            var now = Clock.UtcNowTz;
            var oldStatus = current.ReviewStatus;
            current.ReviewStatus = TaskReviewStatus.ReworkRequested;
            current.ReviewedAt = now;
            current.ReviewedById = dto.ReviewedById;
            current.ReviewedByName = dto.ReviewedByName;
            current.ReworkRemark = dto.ReworkRemark;
            current.ModifiedBy = Actor();
            current.ModifiedDate = now;
            _repo.Update(current);

            _audit.AddAudit("TASK_REVIEW_REWORK", "TaskReview", nameof(TaskReview), eaTaskId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                new { ReviewStatus = oldStatus }, new { current.ReviewCycleNo, current.ReviewStatus, current.ReviewedById, current.ReviewedByName, current.ReworkRemark },
                "Rework requested");

            await _db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
            return ToSummary(current);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<TaskReviewSummaryDto> GetCurrentAsync(long eaTaskId, CancellationToken ct = default)
    {
        var current = await _repo.GetCurrentAsync(eaTaskId, ct);
        return current is null ? NoReviewSummary() : ToSummary(current);
    }

    public async Task<List<TaskReviewHistoryItemDto>> GetHistoryAsync(long eaTaskId, CancellationToken ct = default)
    {
        var history = await _repo.GetHistoryAsync(eaTaskId, ct);
        return history.Select(ToHistoryItem).ToList();
    }

    public async Task<Dictionary<long, TaskReviewSummaryDto>> BatchGetCurrentAsync(IReadOnlyCollection<long> eaTaskIds, CancellationToken ct = default)
    {
        var current = await _repo.GetCurrentBatchAsync(eaTaskIds, ct);
        return eaTaskIds.Distinct().ToDictionary(id => id, id => current.TryGetValue(id, out var r) ? ToSummary(r) : NoReviewSummary());
    }

    private static TaskReviewSummaryDto NoReviewSummary() => new() { Status = null, ReviewCycleNumber = 0 };

    private static TaskReviewSummaryDto ToSummary(TaskReview r) => new()
    {
        Status = r.ReviewStatus,
        ReviewCycleNumber = r.ReviewCycleNo,
        ReviewerId = r.ReviewerId,
        ReviewerName = r.ReviewerName,
        SubmittedById = r.SubmittedById,
        SubmittedByName = r.SubmittedByName,
        SubmittedForReviewAt = r.SubmittedAt,
        ReviewedById = r.ReviewedById,
        ReviewedByName = r.ReviewedByName,
        ReviewedAt = r.ReviewedAt,
        ReviewRemark = r.ReviewRemark,
        ReworkRemark = r.ReworkRemark
    };

    private static TaskReviewHistoryItemDto ToHistoryItem(TaskReview r) => new()
    {
        ReviewCycleNumber = r.ReviewCycleNo,
        Status = r.ReviewStatus,
        ReviewerId = r.ReviewerId,
        ReviewerName = r.ReviewerName,
        SubmittedById = r.SubmittedById,
        SubmittedByName = r.SubmittedByName,
        SubmittedForReviewAt = r.SubmittedAt,
        ReviewedById = r.ReviewedById,
        ReviewedByName = r.ReviewedByName,
        ReviewedAt = r.ReviewedAt,
        ReviewRemark = r.ReviewRemark,
        ReworkRemark = r.ReworkRemark
    };
}
