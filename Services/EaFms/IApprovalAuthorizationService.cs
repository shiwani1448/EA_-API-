namespace Jarvis5.Services.EaFms;

public interface IApprovalAuthorizationService
{
    /// <summary>Return true when the approval request remains eligible for an operation.</summary>
    Task<bool> CanPerformAsync(long approvalRequestId, string operation, CancellationToken ct = default);
}
