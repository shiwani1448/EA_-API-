using Jarvis5.Dtos.Analysis;

namespace Jarvis5.Services;

public interface IAnalysisService
{
    /// <summary>Fetches request + similar-request context, calls the model, and
    /// persists the result as a draft (creating or overwriting the SCIH_Analysis
    /// row) so it can be reloaded/edited via GetByRequestIdAsync. Does not advance
    /// the request stage — only SaveAsync does that.</summary>
    Task<AnalysisDetailDto> GenerateAsync(long requestId, CancellationToken ct = default);

    /// <summary>Persists the user-reviewed/edited analysis and, on first save,
    /// advances the request to Stage 2 (Analysis).</summary>
    Task<AnalysisDetailDto> SaveAsync(long requestId, SaveAnalysisDto dto, CancellationToken ct = default);

    /// <summary>Fetches the previously generated/saved analysis for a request, so
    /// it can be reloaded into the review/edit screen. Throws NotFoundException if
    /// the request doesn't exist or no analysis has been generated/saved for it yet.</summary>
    Task<AnalysisDetailDto> GetByRequestIdAsync(long requestId, CancellationToken ct = default);

    /// <summary>Only usable right after a Director rejection (Status =
    /// ANALYSIS_REWORK). Builds AI context from the latest analysis, the version
    /// before it, the request's own solution design, the Director's rejection
    /// comments/improvement areas, and company knowledge — then asks the AI to
    /// improve (not regenerate) the analysis and stores the result as a brand
    /// new, immutable version.</summary>
    Task<AnalysisDetailDto> ReworkAsync(long requestId, CancellationToken ct = default);

    /// <summary>All analysis versions for a request, oldest first — "View
    /// Previous Versions" on the Approval screen.</summary>
    Task<List<AnalysisDetailDto>> GetVersionsAsync(long requestId, CancellationToken ct = default);
}
