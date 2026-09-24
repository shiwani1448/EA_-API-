using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers.EaFms;

[Authorize]
[ApiController]
[Route("api/ea/delegations/{delegationId:long}/ai")]
public class DelegationAiController : ControllerBase
{
    private readonly IDelegationAiService _delegationAi;

    public DelegationAiController(IDelegationAiService delegationAi)
    {
        _delegationAi = delegationAi;
    }

    /// <summary>Preview only. Suggests a doer purely from historical Delegations of the same
    /// DelegationType — there is no employee/role directory, so this can never name anyone
    /// who wasn't a real doer of a past delegation of the same type. Writes nothing.</summary>
    [HttpPost("suggest-owner")]
    [ProducesResponseType(typeof(DelegationAiOwnerSuggestionResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DelegationAiOwnerSuggestionResponseDto>> SuggestOwner(long delegationId, CancellationToken ct)
    {
        var result = await _delegationAi.SuggestOwnerAsync(delegationId, ct);
        return Ok(result);
    }

    /// <summary>Writes the given doer (the EA's reviewed/edited choice — never re-derived
    /// from Claude) onto the real delegation. 409 once Completed.</summary>
    [HttpPost("suggest-owner/apply")]
    [ProducesResponseType(typeof(DelegationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DelegationResponseDto>> ApplySuggestedOwner(
        long delegationId, [FromBody] ApplySuggestedOwnerRequestDto dto, CancellationToken ct)
    {
        var result = await _delegationAi.ApplySuggestedOwnerAsync(delegationId, dto, ct);
        return Ok(result);
    }

    /// <summary>Preview only. Suggests a due date computed from a configured TAT rule for
    /// this DelegationType, or failing that the real average completion time of similar past
    /// delegations. The date is always computed in code, never by Claude. Writes nothing.</summary>
    [HttpPost("predict-due-date")]
    [ProducesResponseType(typeof(DelegationAiDueDatePredictionResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DelegationAiDueDatePredictionResponseDto>> PredictDueDate(long delegationId, CancellationToken ct)
    {
        var result = await _delegationAi.PredictDueDateAsync(delegationId, ct);
        return Ok(result);
    }

    /// <summary>Writes the given due date (the EA's reviewed/edited choice — never re-derived
    /// from Claude) onto the real delegation's EndDate. 409 once Completed.</summary>
    [HttpPost("predict-due-date/apply")]
    [ProducesResponseType(typeof(DelegationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DelegationResponseDto>> ApplyPredictedDueDate(
        long delegationId, [FromBody] ApplyPredictedDueDateRequestDto dto, CancellationToken ct)
    {
        var result = await _delegationAi.ApplyPredictedDueDateAsync(delegationId, dto, ct);
        return Ok(result);
    }

    /// <summary>Preview only. Assesses delay risk from the Delegation's own real execution
    /// data (status, due-date proximity, TAT usage, pause history). Writes nothing.</summary>
    [HttpPost("delay-risk")]
    [ProducesResponseType(typeof(DelegationAiDelayRiskResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DelegationAiDelayRiskResponseDto>> CheckDelayRisk(long delegationId, CancellationToken ct)
    {
        var result = await _delegationAi.CheckDelayRiskAsync(delegationId, ct);
        return Ok(result);
    }
}
