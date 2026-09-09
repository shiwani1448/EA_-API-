using Jarvis5.Entities;

namespace Jarvis5.Repositories;

public interface IAnalysisRepository
{
    /// <summary>Latest version of the saved analysis for a request (there is
    /// normally exactly one row until a rework has happened).</summary>
    Task<SCIHAnalysis?> GetByRequestIdAsync(long requestId, CancellationToken ct = default);

    Task<SCIHAnalysis?> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>All versions of the analysis for a request, oldest first — used
    /// for the "View Previous Versions" screen. Never empty once a first
    /// analysis has been saved.</summary>
    Task<List<SCIHAnalysis>> GetVersionsByRequestIdAsync(long requestId, CancellationToken ct = default);

    /// <summary>Executive-summary text (from AnalysisJson) keyed by RequestId, for
    /// requests that already have a saved analysis — used as AI prompt context.
    /// Only the latest version per request is considered.</summary>
    Task<Dictionary<long, string>> GetExecutiveSummariesAsync(IReadOnlyCollection<long> requestIds, CancellationToken ct = default);

    Task AddAsync(SCIHAnalysis analysis, CancellationToken ct = default);
    void Update(SCIHAnalysis analysis);
}
