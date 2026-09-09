using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface IFollowupCycleService
{
    Task<FollowupCycleResponseDto> CreateAsync(long followupId, CreateFollowupCycleRequestDto dto, CancellationToken ct = default);
    Task<List<FollowupCycleResponseDto>> GetHistoryAsync(long followupId, CancellationToken ct = default);
    Task<FollowupCycleResponseDto> GetAsync(long followupId, long cycleId, CancellationToken ct = default);
}
