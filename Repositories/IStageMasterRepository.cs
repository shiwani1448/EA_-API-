using Jarvis5.Entities;

namespace Jarvis5.Repositories;

public interface IStageMasterRepository
{
    /// <summary>All active stages, ordered for display — used by the frontend to
    /// dynamically load the stage-selection list.</summary>
    Task<List<SCIHStageMaster>> GetAllActiveAsync(CancellationToken ct = default);

    /// <summary>Looks up specific stages by id, e.g. to validate a module's
    /// selected StageIds and pull their canonical name/checklist server-side.</summary>
    Task<List<SCIHStageMaster>> GetByIdsAsync(IReadOnlyCollection<long> ids, CancellationToken ct = default);
}
