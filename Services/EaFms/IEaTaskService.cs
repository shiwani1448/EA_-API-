using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface IEaTaskService
{
    Task<List<EaTaskResponseDto>> QueryAsync(long? moduleId, string? recordId, CancellationToken ct);
    Task<EaTaskResponseDto> GetAsync(long id, CancellationToken ct);
    Task<EaTaskResponseDto> CreateAsync(CreateEaTaskDto dto, CancellationToken ct);
    Task<EaTaskResponseDto> CreateWithoutTatAsync(CreateEaTaskDto dto, CancellationToken ct);
    /// <summary>Central execution timeline (Created/Started/Paused/Resumed/Completed/Cancelled)
    /// reconstructed read-only from existing authoritative sources. Not the module's full
    /// business history.</summary>
    Task<List<EaTaskHistoryEventDto>> GetHistoryAsync(long id, CancellationToken ct);
}
