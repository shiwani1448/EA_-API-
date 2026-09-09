using Jarvis5.Entities;

namespace Jarvis5.Repositories;

public interface ISolutionDesignRepository
{
    Task<SCIHSolutionDesign?> GetByRequestIdAsync(long requestId, CancellationToken ct = default);

    /// <summary>Saved solution designs for the given request ids, keyed by RequestId —
    /// used to find reusable assets from previous similar requests ahead of AI generation.</summary>
    Task<Dictionary<long, SCIHSolutionDesign>> GetByRequestIdsAsync(IReadOnlyCollection<long> requestIds, CancellationToken ct = default);

    Task AddAsync(SCIHSolutionDesign design, CancellationToken ct = default);
    void Update(SCIHSolutionDesign design);
}
