using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers.EaFms;

[ApiController]
[Route("api/ea/calendar/ai")]
public class CalendarAiController : ControllerBase
{
    private readonly ICalendarAiService _calendarAi;

    public CalendarAiController(ICalendarAiService calendarAi)
    {
        _calendarAi = calendarAi;
    }

    /// <summary>Preview only. Parses free text (e.g. "Director travel to Chennai from 25 to
    /// 26 Oct") into structured event fields. Writes nothing.</summary>
    [HttpPost("quick-add")]
    [ProducesResponseType(typeof(CalendarAiQuickAddResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarAiQuickAddResponseDto>> QuickAdd([FromBody] QuickAddCalendarEventRequestDto dto, CancellationToken ct)
    {
        var result = await _calendarAi.QuickAddAsync(dto, ct);
        return Ok(result);
    }

    /// <summary>Creates the real calendar event from the EA's reviewed (or fully retyped)
    /// fields — never re-derived or re-calls Claude.</summary>
    [HttpPost("quick-add/apply")]
    [ProducesResponseType(typeof(CalendarEventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CalendarEventDto>> ApplyQuickAdd([FromBody] ApplyQuickAddSuggestionRequestDto dto, CancellationToken ct)
    {
        var result = await _calendarAi.ApplyQuickAddAsync(dto, ct);
        return CreatedAtAction("GetEvent", "Calendar", new { id = result.Id }, result);
    }

    /// <summary>Preview only. Detects overlapping events by exact time comparison over the
    /// EA's own real calendar rows in [from, to) and narrates them. Writes nothing.</summary>
    [HttpPost("conflict-check")]
    [ProducesResponseType(typeof(CalendarAiConflictCheckResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CalendarAiConflictCheckResponseDto>> CheckConflicts([FromBody] CalendarConflictCheckRequestDto dto, CancellationToken ct)
    {
        var result = await _calendarAi.CheckConflictsAsync(dto, ct);
        return Ok(result);
    }
}
