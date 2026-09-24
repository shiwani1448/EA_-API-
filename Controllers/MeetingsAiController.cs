using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Jarvis5.Services;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/meetings/{meetingId:long}/ai")]
public class MeetingsAiController : ControllerBase
{
    private readonly IMeetingAiService _meetingAi;
    private readonly ICurrentUserService _user;

    public MeetingsAiController(IMeetingAiService meetingAi, ICurrentUserService user)
    {
        _meetingAi = meetingAi;
        _user = user;
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

    /// <summary>Saves reviewed action items and creates their delegations atomically.
    /// Accepts existing action IDs and new manual or AI-proposed rows after completion.</summary>
    [HttpPost("actions/confirm")]
    [ProducesResponseType(typeof(MeetingAiActionsConfirmResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MeetingAiActionsConfirmResponseDto>> ConfirmActions(
        long meetingId, [FromBody] ConfirmMeetingAiActionsRequestDto? dto, CancellationToken ct)
    {
        dto ??= new ConfirmMeetingAiActionsRequestDto();
        var actor = _user.UserName ?? _user.UserId.ToString();
        var result = await _meetingAi.ConfirmActionsAsync(meetingId, dto, actor, ct);
        return Ok(result);
    }
}
