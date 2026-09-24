using Jarvis5.Entities.EaFms;
using Microsoft.AspNetCore.Http;

namespace Jarvis5.Services.EaFms;

public interface IApprovalLifecycleService
{
    Task<Dtos.EaFms.ApprovalDetailDto> StartActualAsync(long approvalRequestId, CancellationToken ct = default);
    Task<Dtos.EaFms.ApprovalDetailDto> StartReviewAsync(long approvalRequestId, CancellationToken ct = default);
    Task<Dtos.EaFms.ApprovalDetailDto> StartReworkAsync(long approvalRequestId, CancellationToken ct = default);

    Task<ApprovalRequest> SubmitAsync(long approvalRequestId, CancellationToken ct = default);
    Task<ApprovalRequest> ApproveAsync(long approvalRequestId, Dtos.EaFms.ApprovalDecisionDto dto, CancellationToken ct = default);
    Task<ApprovalRequest> RejectAsync(long approvalRequestId, Dtos.EaFms.ApprovalDecisionDto dto, CancellationToken ct = default);
    Task<ApprovalRequest> RequestChangesAsync(long approvalRequestId, Dtos.EaFms.ApprovalDecisionDto dto, CancellationToken ct = default);
    Task<ApprovalRequest> ResubmitAsync(long approvalRequestId, CancellationToken ct = default);
    Task<List<Entities.EaFms.ApprovalCycle>> GetCyclesAsync(long approvalRequestId, CancellationToken ct = default);
    Task<List<Jarvis5.Dtos.EaFms.WorkflowHistoryResponseDto>> GetHistoryAsync(long approvalRequestId, CancellationToken ct = default);
    Task<bool> IsDocumentOperationAllowed(long approvalRequestId, string operation, CancellationToken ct = default);

    /// <summary>
    /// Pause/Resume an explicitly started, InProgress Approval. Business WorkflowStatus is separate.
    /// Uses the same shared WorkPause
    /// architecture as Delegation's, with a lazily-created pause anchor WorkflowInstance. Unlike
    /// Delegation, which returns its own DelegationResponseDto, these return the same ApprovalDetailDto
    /// the GET-by-id endpoint returns, via ApprovalQueryService.DetailAsync.
    /// </summary>
    Task<Dtos.EaFms.ApprovalDetailDto> PauseAsync(long approvalRequestId, Dtos.EaFms.ApprovalPauseRequestDto? request, CancellationToken ct = default);
    Task<Dtos.EaFms.ApprovalDetailDto> ResumeAsync(long approvalRequestId, CancellationToken ct = default);

    /// <summary>
    /// Task Review/Rework (Phase 1): resolves the Approval Request's EaTask and delegates to the
    /// shared ITaskReviewService, with per-phase TAT bookkeeping (ApprovalPhaseTat) as a side effect.
    /// Entirely separate from Submit/Approve/Reject/RequestChanges/Resubmit above (the existing
    /// WorkflowStatus/ApprovalCycle business lifecycle) — review/approve and review/rework never touch
    /// WorkflowStatus/ApprovalCycle/eaTask.ExecutionStatus.
    /// </summary>
    Task<Dtos.EaFms.TaskReviewSummaryDto> SubmitForReviewAsync(long approvalRequestId, Dtos.EaFms.SubmitForReviewRequestDto dto, CancellationToken ct = default);
    Task<Dtos.EaFms.TaskReviewSummaryDto> ApproveReviewAsync(long approvalRequestId, Dtos.EaFms.ApproveTaskReviewRequestDto dto, IFormFile? attachment, CancellationToken ct = default);
    Task<Dtos.EaFms.TaskReviewSummaryDto> RequestTaskReworkAsync(long approvalRequestId, Dtos.EaFms.RequestTaskReworkRequestDto dto, IFormFile? attachment, CancellationToken ct = default);
    Task<List<Dtos.EaFms.TaskReviewHistoryItemDto>> GetReviewHistoryAsync(long approvalRequestId, CancellationToken ct = default);
}
