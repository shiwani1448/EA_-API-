using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers.EaFms;

/// <summary>AI assistance for the New Delegation form, before any delegation row exists.
/// Preview only — the EA applies a suggestion by editing the form, so there is no /apply.</summary>
[ApiController]
[Route("api/ea/delegations/ai")]
public class DelegationDraftAiController : ControllerBase
{
    private readonly IDelegationAiService _delegationAi;

    public DelegationDraftAiController(IDelegationAiService delegationAi)
    {
        _delegationAi = delegationAi;
    }

    /// <summary>Preview only. Suggests a doer from historical Delegations of the draft's
    /// DelegationType. Writes nothing.</summary>
    [HttpPost("suggest-owner")]
    [ProducesResponseType(typeof(DelegationAiOwnerSuggestionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DelegationAiOwnerSuggestionResponseDto>> SuggestOwner(
        [FromBody] DelegationAiDraftRequestDto dto, CancellationToken ct)
    {
        var result = await _delegationAi.SuggestOwnerForDraftAsync(dto, ct);
        return Ok(result);
    }

    /// <summary>Preview only. Suggests a due date from a configured TAT rule or the real
    /// average of past delegations of the draft's DelegationType. Writes nothing.</summary>
    [HttpPost("predict-due-date")]
    [ProducesResponseType(typeof(DelegationAiDueDatePredictionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DelegationAiDueDatePredictionResponseDto>> PredictDueDate(
        [FromBody] DelegationAiDraftRequestDto dto, CancellationToken ct)
    {
        var result = await _delegationAi.PredictDueDateForDraftAsync(dto, ct);
        return Ok(result);
    }
}
