using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers.EaFms;

/// <summary>
/// EA FMS Calendar: a standalone tool, exactly like Google Calendar. The EA types every
/// entry in herself; it is never an aggregation of Meeting/Delegation/Approval/Travel/
/// Followup, which track the EA's own operational work rather than the Director's schedule.
/// </summary>
[ApiController]
[Route("api/ea/calendar")]
public class CalendarController : ControllerBase
{
    private readonly ICalendarService _calendar;

    public CalendarController(ICalendarService calendar)
    {
        _calendar = calendar;
    }

    /// <summary>Lists the EA's calendar for [from, to): her own entries plus her work from the modules, shown
    /// automatically and read live — Meetings at their date/time, Delegations start→due, Approvals on their required
    /// date, Travel departure→return, Follow-ups at their due time (read-only; Source + SourceRecordId open the record).
    /// eventTypes (ClientMeeting, InternalMeeting, Personal, Travel, Task) and sources (Calendar, Meeting, Delegation,
    /// Approval, Travel, Follow-up) are optional whitelists; includeLinked=false shows only her own entries.
    /// employeeId/employeeName pick whose calendar (default: the logged-in EA).</summary>
    [HttpGet("events")]
    [ProducesResponseType(typeof(List<CalendarEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<CalendarEventDto>>> GetEvents([FromQuery] CalendarEventsQueryDto query, CancellationToken ct)
        => Ok(await _calendar.GetEventsAsync(query, ct));

    /// <summary>Meeting Statistics panel counts (total/completed/upcoming/scheduled hours) for [from, to).</summary>
    [HttpGet("events/stats")]
    [ProducesResponseType(typeof(CalendarStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CalendarStatsDto>> GetStats([FromQuery] CalendarEventsQueryDto query, CancellationToken ct)
        => Ok(await _calendar.GetStatsAsync(query, ct));

    /// <summary>Creates a calendar event.</summary>
    [HttpPost("events")]
    [ProducesResponseType(typeof(CalendarEventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CalendarEventDto>> CreateEvent([FromBody] CreateCalendarEventDto dto, CancellationToken ct)
    {
        var result = await _calendar.CreateEventAsync(dto, ct);
        return CreatedAtAction(nameof(GetEvent), new { id = result.Id }, result);
    }

    /// <summary>Gets a single calendar event by id.</summary>
    [HttpGet("events/{id:long}")]
    [ProducesResponseType(typeof(CalendarEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CalendarEventDto>> GetEvent(long id, CancellationToken ct)
        => Ok(await _calendar.GetEventAsync(id, ct));

    /// <summary>Updates a calendar event.</summary>
    [HttpPut("events/{id:long}")]
    [ProducesResponseType(typeof(CalendarEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CalendarEventDto>> UpdateEvent(long id, [FromBody] UpdateCalendarEventDto dto, CancellationToken ct)
        => Ok(await _calendar.UpdateEventAsync(id, dto, ct));

    /// <summary>Deletes a calendar event.</summary>
    [HttpDelete("events/{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEvent(long id, CancellationToken ct)
    {
        await _calendar.DeleteEventAsync(id, ct);
        return NoContent();
    }
}
