using Jarvis5.Dtos;
using Jarvis5.Entities;

namespace Jarvis5.Repositories;

public interface IRequestRepository
{
    Task<string> GenerateNextRequestNoAsync(CancellationToken ct = default);
    Task<SCIHRequest?> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>Existence check that ignores the soft-delete filter — used to
    /// distinguish "never existed" (404) from "exists but deleted" when callers
    /// (e.g. the history endpoint) must keep working for deleted requests.</summary>
    Task<bool> ExistsIgnoringSoftDeleteAsync(long id, CancellationToken ct = default);

    Task AddAsync(SCIHRequest request, CancellationToken ct = default);
    void Update(SCIHRequest request);
    Task<(List<SCIHRequest> Items, int TotalCount)> GetPagedAsync(RequestFilterDto filter, CancellationToken ct = default);

    /// <summary>Other non-deleted requests, most recent first, for similarity scoring
    /// ahead of AI analysis. Capped so scoring stays cheap as the table grows.</summary>
    Task<List<SCIHRequest>> GetSimilarityCandidatesAsync(long excludeRequestId, int maxCandidates, CancellationToken ct = default);
}
