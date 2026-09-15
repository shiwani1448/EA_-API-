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
}
