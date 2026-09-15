namespace Jarvis5.Services.EaFms;

public interface IApprovalAuthorizationService
{
    /// <summary>Return true if current user may perform the given operation on the approval request.</summary>
    Task<bool> CanPerformAsync(long approvalRequestId, string operation, CancellationToken ct = default);
}
