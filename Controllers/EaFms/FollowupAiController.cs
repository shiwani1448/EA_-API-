using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers.EaFms;

[Authorize]
[ApiController]
[Route("api/ea/followups/{followupId:long}/ai")]
public class FollowupAiController : ControllerBase
{
    private readonly IFollowupAiService _followupAi;

    public FollowupAiController(IFollowupAiService followupAi)
    {
        _followupAi = followupAi;
    }

    /// <summary>Preview only. Drafts a reminder subject/body from this follow-up's own
    /// fields. Writes nothing, sends nothing.</summary>
    [HttpPost("reminder")]
    [ProducesResponseType(typeof(FollowupAiReminderSuggestionResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FollowupAiReminderSuggestionResponseDto>> SuggestReminder(long followupId, CancellationToken ct)
    {
        var result = await _followupAi.SuggestReminderAsync(followupId, ct);
        return Ok(result);
    }

    /// <summary>Sends the EA's reviewed (or fully retyped) subject/body via the real EA
    /// reminder SMTP sender. This is an actual delivery, not a mailto: handoff.</summary>
    [HttpPost("reminder/send")]
    [ProducesResponseType(typeof(FollowupAiReminderSentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FollowupAiReminderSentResponseDto>> SendReminder(long followupId, [FromBody] SendFollowupReminderRequestDto dto, CancellationToken ct)
    {
        var result = await _followupAi.SendSuggestedReminderAsync(followupId, dto, ct);
        return Ok(result);
    }

    /// <summary>Preview only. Recommends whether/to which level to escalate, based only on
    /// this follow-up's overdue status, attempt count, and current escalation state. Writes
    /// nothing.</summary>
    [HttpPost("suggest-escalation")]
    [ProducesResponseType(typeof(FollowupAiEscalationSuggestionResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FollowupAiEscalationSuggestionResponseDto>> SuggestEscalation(long followupId, CancellationToken ct)
    {
        var result = await _followupAi.SuggestEscalationAsync(followupId, ct);
        return Ok(result);
    }

    /// <summary>Creates the real Escalation at the EA's reviewed (or overridden) level —
    /// never re-derived from Claude.</summary>
    [HttpPost("suggest-escalation/apply")]
    [ProducesResponseType(typeof(EscalationResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EscalationResponseDto>> ApplySuggestedEscalation(long followupId, [FromBody] ApplySuggestedEscalationRequestDto dto, CancellationToken ct)
    {
        var result = await _followupAi.ApplySuggestedEscalationAsync(followupId, dto, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Preview only. Estimates a resolution date from past follow-ups of the same
    /// type. Writes nothing — Followup has no field this could be applied into.</summary>
    [HttpPost("predict-resolution")]
    [ProducesResponseType(typeof(FollowupAiResolutionPredictionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FollowupAiResolutionPredictionResponseDto>> PredictResolution(long followupId, CancellationToken ct)
    {
        var result = await _followupAi.PredictResolutionTimeAsync(followupId, ct);
        return Ok(result);
    }

    /// <summary>Preview only. Judges how at-risk this follow-up is of never getting resolved.
    /// Writes nothing.</summary>
    [HttpPost("at-risk")]
    [ProducesResponseType(typeof(FollowupAiAtRiskResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FollowupAiAtRiskResponseDto>> CheckAtRisk(long followupId, CancellationToken ct)
    {
        var result = await _followupAi.CheckAtRiskAsync(followupId, ct);
        return Ok(result);
    }
}
