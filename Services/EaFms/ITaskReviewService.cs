using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// Central Task Review/Rework engine, operating purely on EaTaskId. No module (Delegation and Approval) may duplicate this state machine — each resolves its own
/// EaTaskId and delegates here. There is no generic public controller; only module-specific
/// routes call this service.
/// </summary>
public interface ITaskReviewService
{
    Task<TaskReviewSummaryDto> SubmitForReviewAsync(long eaTaskId, SubmitForReviewRequestDto dto, CancellationToken ct = default);
    Task<TaskReviewSummaryDto> ApproveAsync(long eaTaskId, ApproveTaskReviewRequestDto dto, CancellationToken ct = default);
    Task<TaskReviewSummaryDto> RequestReworkAsync(long eaTaskId, RequestTaskReworkRequestDto dto, CancellationToken ct = default);

    /// <summary>Never null — returns the "no review yet" shape (Status=null, ReviewCycleNumber=0) when nothing was ever submitted.</summary>
    Task<TaskReviewSummaryDto> GetCurrentAsync(long eaTaskId, CancellationToken ct = default);

    /// <summary>Full cycle history, oldest (CycleNo 1) first.</summary>
    Task<List<TaskReviewHistoryItemDto>> GetHistoryAsync(long eaTaskId, CancellationToken ct = default);

    /// <summary>Latest-cycle summary per EaTask in one batch — for list/EM-Report style callers. No N+1.</summary>
    Task<Dictionary<long, TaskReviewSummaryDto>> BatchGetCurrentAsync(IReadOnlyCollection<long> eaTaskIds, CancellationToken ct = default);
}
