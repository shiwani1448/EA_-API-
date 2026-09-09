using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface IMeetingService
{
    Task<MeetingDetailResponseDto> CreateAsync(CreateMeetingRequestDto dto, CancellationToken ct = default);
    Task<List<MeetingListItemResponseDto>> QueryAsync(string? search = null, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<MeetingDetailResponseDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task UpdateAsync(long id, UpdateMeetingRequestDto dto, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}
