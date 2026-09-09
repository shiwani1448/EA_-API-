using Jarvis5.Dtos.Approval;

namespace Jarvis5.Services;

public interface IApprovalService
{
    /// <summary>Creates a new Pending approval round and moves the request to
    /// Stage 4 (Approval). Requires the Solution Design to already be approved
    /// (Stage 3 complete).</summary>
    Task<ApprovalDetailDto> SubmitForApprovalAsync(long requestId, CancellationToken ct = default);

    /// <summary>Approves the current Pending round and moves the request to
    /// Stage 5 (Development).</summary>
    Task<ApprovalDetailDto> ApproveAsync(long requestId, ApproveRequestDto dto, CancellationToken ct = default);

    /// <summary>Rejects the current Pending round, increments the rework count,
    /// and sends the request back to Stage 2 (Analysis) for rework.</summary>
    Task<ApprovalDetailDto> RejectAsync(long requestId, RejectRequestDto dto, CancellationToken ct = default);

    /// <summary>Aggregated data for the Approval screen.</summary>
    Task<ApprovalDetailDto> GetDetailAsync(long requestId, CancellationToken ct = default);

    /// <summary>Full approval round history for a request, oldest first.</summary>
    Task<List<ApprovalRoundDto>> GetRoundsAsync(long requestId, CancellationToken ct = default);
}
