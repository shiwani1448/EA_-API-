using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers.EaFms;

[ApiController]
[Route("api/ea/travel/requests/{travelRequestId:long}/ai")]
public class TravelAiController : ControllerBase
{
    private readonly ITravelAiService _travelAi;

    public TravelAiController(ITravelAiService travelAi)
    {
        _travelAi = travelAi;
    }

    /// <summary>Preview only. Suggests candidate travel/hotel options from the request's
    /// own stated route/dates/preferences. Estimated costs are illustrative only. Creates
    /// no TravelBooking and never changes Travel lifecycle/approval/TAT.</summary>
    [HttpPost("options/suggest")]
    [ProducesResponseType(typeof(TravelAiOptionsSuggestionResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TravelAiOptionsSuggestionResponseDto>> SuggestOptions(long travelRequestId, CancellationToken ct)
    {
        var result = await _travelAi.SuggestOptionsAsync(travelRequestId, ct);
        return Ok(result);
    }

    /// <summary>EA confirmation of (possibly edited) suggested options. Creates real
    /// TravelBooking rows via the existing booking-creation service — never calls Claude.</summary>
    [HttpPost("options/confirm")]
    [ProducesResponseType(typeof(TravelAiOptionsConfirmResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TravelAiOptionsConfirmResponseDto>> ConfirmOptions(
        long travelRequestId, [FromBody] ConfirmTravelAiOptionsRequestDto? dto, CancellationToken ct)
    {
        dto ??= new ConfirmTravelAiOptionsRequestDto();
        var result = await _travelAi.ConfirmOptionsAsync(travelRequestId, dto, ct);
        return Ok(result);
    }

    /// <summary>Preview only. Compares EA-supplied, already-real-priced options against
    /// the trip's stated preferences and recommends one. Claude never alters a submitted
    /// price/provider/detail. Creates no TravelBooking — the EA still uses
    /// /ai/options/confirm afterward.</summary>
    [HttpPost("options/compare")]
    [ProducesResponseType(typeof(TravelAiCompareOptionsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TravelAiCompareOptionsResponseDto>> CompareOptions(
        long travelRequestId, [FromBody] TravelAiCompareOptionsRequestDto? dto, CancellationToken ct)
    {
        dto ??= new TravelAiCompareOptionsRequestDto();
        var result = await _travelAi.CompareOptionsAsync(travelRequestId, dto, ct);
        return Ok(result);
    }

    /// <summary>Preview only. Drafts a narrative itinerary. Writes nothing — to keep it,
    /// the EA saves it themselves via the existing Travel Request update endpoint
    /// (ItineraryNotes), same as if they'd typed it.</summary>
    [HttpPost("itinerary/draft")]
    [ProducesResponseType(typeof(TravelAiItineraryResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TravelAiItineraryResponseDto>> DraftItinerary(long travelRequestId, CancellationToken ct)
    {
        var result = await _travelAi.DraftItineraryAsync(travelRequestId, ct);
        return Ok(result);
    }

    /// <summary>Preview only. Drafts a packing/documents checklist. Purely advisory —
    /// nothing to persist, no confirm step.</summary>
    [HttpPost("checklist")]
    [ProducesResponseType(typeof(TravelAiChecklistResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TravelAiChecklistResponseDto>> DraftChecklist(long travelRequestId, CancellationToken ct)
    {
        var result = await _travelAi.DraftChecklistAsync(travelRequestId, ct);
        return Ok(result);
    }
}
