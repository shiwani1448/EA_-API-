using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface IBusinessModuleService
{
    Task<IReadOnlyList<BusinessModuleDto>> GetAllAsync(bool activeOnly, CancellationToken ct);
    Task<BusinessModuleDto> GetByIdAsync(long id, CancellationToken ct);
    Task<BusinessModuleDto> CreateAsync(SaveBusinessModuleDto dto, CancellationToken ct);
    /// <summary>Safe "delete": sets IsActive=false and keeps every historical reference (no physical delete).</summary>
    Task<BusinessModuleDto> DeactivateAsync(long id, EaActorRequestDto? actor, CancellationToken ct);
    Task<BusinessModuleDto> UpdateAsync(long id, SaveBusinessModuleDto dto, CancellationToken ct);
}
