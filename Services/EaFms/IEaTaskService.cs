using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface IEaTaskService
{
    Task<List<EaTaskResponseDto>> QueryAsync(long? moduleId, string? recordId, CancellationToken ct);
    Task<EaTaskResponseDto> GetAsync(long id, CancellationToken ct);
    Task<EaTaskResponseDto> CreateAsync(CreateEaTaskDto dto, CancellationToken ct);
    Task<EaTaskResponseDto> CreateWithoutTatAsync(CreateEaTaskDto dto, CancellationToken ct);
}
