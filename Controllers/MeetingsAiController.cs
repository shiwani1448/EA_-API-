using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/meetings/{meetingId:long}/ai")]
public class MeetingsAiController : ControllerBase
{
    private readonly IMeetingAiService _meetingAi;

    public MeetingsAiController(IMeetingAiService meetingAi)
    {
        _meetingAi = meetingAi;
    }

    /// <summary>Preview only. Analyzes the Meeting's existing completion evidence
    /// (CompletionMom and/or the extracted CompletionPdfAttachmentId text) and returns
    /// AI-proposed action points for EA review. Creates no MeetingAction, Delegation or
    /// EaTask row, and never changes Meeting/TAT/completion state.</summary>
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(MeetingAiAnalysisResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MeetingAiAnalysisResponseDto>> Analyze(long meetingId, CancellationToken ct)
    {
        var result = await _meetingAi.AnalyzeAsync(meetingId, ct);
        return Ok(result);
    }

    /// <summary>EA confirmation of (possibly edited) AI-proposed actions. Creates real
    /// MeetingAction rows from exactly the submitted values — never the original AI
    /// suggestion — using the same creation logic as the manual actions endpoint. Never
    /// calls Claude and never creates a Delegation/EaTask directly.</summary>
    [HttpPost("actions/confirm")]
    [ProducesResponseType(typeof(MeetingAiActionsConfirmResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MeetingAiActionsConfirmResponseDto>> ConfirmActions(
        long meetingId, [FromBody] ConfirmMeetingAiActionsRequestDto? dto, CancellationToken ct)
    {
        dto ??= new ConfirmMeetingAiActionsRequestDto();
        var actor = User?.Identity?.Name ?? string.Empty;
        var result = await _meetingAi.ConfirmActionsAsync(meetingId, dto, actor, ct);
        return Ok(result);
    }
}
