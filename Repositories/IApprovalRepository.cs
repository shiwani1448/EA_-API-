using Jarvis5.Entities;

namespace Jarvis5.Repositories;

public interface IApprovalRepository
{
    /// <summary>Most recent approval round for a request (highest ApprovalRound),
    /// or null if the request has never been submitted for approval.</summary>
    Task<SCIHApproval?> GetLatestByRequestIdAsync(long requestId, CancellationToken ct = default);

    /// <summary>All approval rounds for a request, oldest first — used for the
    /// "View Rework History" screen.</summary>
    Task<List<SCIHApproval>> GetAllByRequestIdAsync(long requestId, CancellationToken ct = default);

    Task AddAsync(SCIHApproval approval, CancellationToken ct = default);
    void Update(SCIHApproval approval);
}
