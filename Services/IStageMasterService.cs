using Jarvis5.Dtos.DevelopmentPlan;

namespace Jarvis5.Services;

public interface IStageMasterService
{
    /// <summary>All active stages the frontend can offer for a module's stage selection.</summary>
    Task<List<StageMasterDto>> GetAllAsync(CancellationToken ct = default);
}
