using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface IFollowupSourceResolver
{
    Task<long> GetMeetingModuleIdAsync(CancellationToken ct = default);
    Task<FollowupSourceResponseDto> ResolveAsync(long? businessModuleId, string? businessRecordId, CancellationToken ct = default);
}
