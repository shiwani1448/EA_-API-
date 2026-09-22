using Jarvis5.Entities.EaFms;

namespace Jarvis5.Repositories.EaFms;

public interface ITaskReviewRepository
{
    Task AddAsync(TaskReview review, CancellationToken ct = default);

    /// <summary>Latest cycle (highest ReviewCycleNo) for one EaTask, or null if never submitted.</summary>
    Task<TaskReview?> GetCurrentAsync(long eaTaskId, CancellationToken ct = default);

    /// <summary>All cycles for one EaTask, oldest first.</summary>
    Task<List<TaskReview>> GetHistoryAsync(long eaTaskId, CancellationToken ct = default);

    /// <summary>Latest cycle per EaTask, batched for list/EM-Report style callers — no N+1.</summary>
    Task<Dictionary<long, TaskReview>> GetCurrentBatchAsync(IReadOnlyCollection<long> eaTaskIds, CancellationToken ct = default);

    void Update(TaskReview review);
}
