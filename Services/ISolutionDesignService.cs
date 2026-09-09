using Jarvis5.Dtos.SolutionDesign;

namespace Jarvis5.Services;

public interface ISolutionDesignService
{
    /// <summary>Fetches request + approved analysis + company-knowledge context, calls
    /// the model, and persists the result as a draft (creating or overwriting the
    /// SCIH_SolutionDesign row). Does not advance the request stage — only
    /// ApproveAsync does that.</summary>
    Task<SolutionDesignDetailDto> GenerateAsync(long requestId, CancellationToken ct = default);

    /// <summary>Fetches the previously generated/edited solution design for a request,
    /// so it can be reloaded into the review/edit screen.</summary>
    Task<SolutionDesignDetailDto> GetByRequestIdAsync(long requestId, CancellationToken ct = default);

    /// <summary>Persists the user-reviewed/edited solution design in place (marks it
    /// IsEdited, moves Status to Reviewed, bumps Version).</summary>
    Task<SolutionDesignDetailDto> UpdateAsync(long requestId, UpdateSolutionDesignDto dto, CancellationToken ct = default);

    /// <summary>Locks the solution design (Status = Approved) and advances the request
    /// to Stage 3 (Solution Design).</summary>
    Task<SolutionDesignDetailDto> ApproveAsync(long requestId, ApproveSolutionDesignDto? dto, CancellationToken ct = default);
}
