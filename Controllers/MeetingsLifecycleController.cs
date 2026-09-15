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
    public async Task<IActionResult> Start(long meetingId, CancellationToken ct)
        => Ok(await _lifecycle.StartAsync(meetingId, new MeetingStartRequestDto(), ct));

    /// <summary>Pause Meeting Task</summary>
    [HttpPost("pause")]
    [ProducesResponseType(typeof(MeetingPauseResponseDto), 200)]
    public async Task<IActionResult> Pause(long meetingId, CancellationToken ct)
        => Ok(await _lifecycle.PauseAsync(meetingId, new MeetingPauseRequestDto(), ct));

    /// <summary>Resume Meeting Task</summary>
    [HttpPost("resume")]
    [ProducesResponseType(typeof(MeetingPauseResponseDto), 200)]
    public async Task<IActionResult> Resume(long meetingId, CancellationToken ct)
        => Ok(await _lifecycle.ResumeAsync(meetingId, new MeetingResumeRequestDto(), ct));

    /// <summary>Complete Meeting Task</summary>
    [HttpPost("complete")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(27 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 27 * 1024 * 1024)]
    [ProducesResponseType(typeof(MeetingLifecycleResponseDto), 200)]
    public async Task<IActionResult> Complete(long meetingId, [FromForm] MeetingCompleteRequestDto dto, CancellationToken ct)
        => Ok(await _lifecycle.CompleteAsync(meetingId, dto, ct));

}
