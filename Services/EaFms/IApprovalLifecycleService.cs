using Jarvis5.Entities.EaFms;

namespace Jarvis5.Services.EaFms;

public interface IApprovalLifecycleService
{
    Task<ApprovalRequest> SubmitAsync(long approvalRequestId, CancellationToken ct = default);
    Task<ApprovalRequest> ApproveAsync(long approvalRequestId, Dtos.EaFms.ApprovalDecisionDto dto, CancellationToken ct = default);
    Task<ApprovalRequest> RejectAsync(long approvalRequestId, Dtos.EaFms.ApprovalDecisionDto dto, CancellationToken ct = default);
    Task<ApprovalRequest> RequestChangesAsync(long approvalRequestId, Dtos.EaFms.ApprovalDecisionDto dto, CancellationToken ct = default);
    Task<ApprovalRequest> ResubmitAsync(long approvalRequestId, CancellationToken ct = default);
    Task<List<Entities.EaFms.ApprovalCycle>> GetCyclesAsync(long approvalRequestId, CancellationToken ct = default);
    Task<List<Jarvis5.Dtos.EaFms.WorkflowHistoryResponseDto>> GetHistoryAsync(long approvalRequestId, CancellationToken ct = default);
    Task<bool> IsDocumentOperationAllowed(long approvalRequestId, string operation, CancellationToken ct = default);

    /// <summary>
    /// Task Review/Rework (Phase 1): resolves the Approval Request's EaTask and delegates to the
    /// shared ITaskReviewService. Entirely separate from Submit/Approve/Reject/RequestChanges/
    /// Resubmit above (the existing WorkflowStatus/ApprovalCycle business lifecycle).
    /// </summary>
    Task<Dtos.EaFms.TaskReviewSummaryDto> SubmitForReviewAsync(long approvalRequestId, Dtos.EaFms.SubmitForReviewRequestDto dto, CancellationToken ct = default);
    Task<Dtos.EaFms.TaskReviewSummaryDto> ApproveReviewAsync(long approvalRequestId, Dtos.EaFms.ApproveTaskReviewRequestDto dto, CancellationToken ct = default);
    Task<Dtos.EaFms.TaskReviewSummaryDto> RequestTaskReworkAsync(long approvalRequestId, Dtos.EaFms.RequestTaskReworkRequestDto dto, CancellationToken ct = default);
    Task<List<Dtos.EaFms.TaskReviewHistoryItemDto>> GetReviewHistoryAsync(long approvalRequestId, CancellationToken ct = default);
}
