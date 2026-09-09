using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

/// <summary>
/// Meeting module-specific lifecycle APIs. Resolves MeetingId → WorkflowInstanceId
/// and delegates to the shared internal lifecycle engine.
/// </summary>
[ApiController]
[Route("api/ea/meetings/{meetingId:long}")]
public class MeetingsLifecycleController : ControllerBase
{
    private readonly IMeetingLifecycleService _lifecycle;

    public MeetingsLifecycleController(IMeetingLifecycleService lifecycle) => _lifecycle = lifecycle;

    /// <summary>Start Meeting Task</summary>
    [HttpPost("start")]
    [ProducesResponseType(typeof(MeetingLifecycleResponseDto), 200)]
    public async Task<IActionResult> Start(long meetingId, [FromBody] MeetingStartRequestDto dto, CancellationToken ct)
        => Ok(await _lifecycle.StartAsync(meetingId, dto, ct));

    /// <summary>Pause Meeting Task</summary>
    [HttpPost("pause")]
    [ProducesResponseType(typeof(MeetingPauseResponseDto), 200)]
    public async Task<IActionResult> Pause(long meetingId, [FromBody] MeetingPauseRequestDto dto, CancellationToken ct)
        => Ok(await _lifecycle.PauseAsync(meetingId, dto, ct));

    /// <summary>Resume Meeting Task</summary>
    [HttpPost("resume")]
    [ProducesResponseType(typeof(MeetingPauseResponseDto), 200)]
    public async Task<IActionResult> Resume(long meetingId, [FromBody] MeetingResumeRequestDto dto, CancellationToken ct)
        => Ok(await _lifecycle.ResumeAsync(meetingId, dto, ct));

    /// <summary>Complete Meeting Task</summary>
    [HttpPost("complete")]
    [ProducesResponseType(typeof(MeetingLifecycleResponseDto), 200)]
    public async Task<IActionResult> Complete(long meetingId, [FromBody] MeetingCompleteRequestDto dto, CancellationToken ct)
        => Ok(await _lifecycle.CompleteAsync(meetingId, dto, ct));

}
