using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface IBusinessModuleService
{
    Task<IReadOnlyList<BusinessModuleDto>> GetAllAsync(bool activeOnly, CancellationToken ct);
    Task<BusinessModuleDto> GetByIdAsync(long id, CancellationToken ct);
    Task<BusinessModuleDto> CreateAsync(SaveBusinessModuleDto dto, CancellationToken ct);
    Task<BusinessModuleDto> UpdateAsync(long id, SaveBusinessModuleDto dto, CancellationToken ct);
}
