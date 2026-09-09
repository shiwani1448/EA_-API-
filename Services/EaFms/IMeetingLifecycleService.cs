using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface IMeetingLifecycleService
{
    Task<MeetingLifecycleResponseDto> StartAsync(long meetingId, MeetingStartRequestDto dto, CancellationToken ct);
    Task<MeetingPauseResponseDto> PauseAsync(long meetingId, MeetingPauseRequestDto dto, CancellationToken ct);
    Task<MeetingPauseResponseDto> ResumeAsync(long meetingId, MeetingResumeRequestDto dto, CancellationToken ct);
    Task<MeetingLifecycleResponseDto> CompleteAsync(long meetingId, MeetingCompleteRequestDto dto, CancellationToken ct);
}
