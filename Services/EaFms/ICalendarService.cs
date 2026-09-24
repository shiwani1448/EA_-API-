using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// The Calendar module: a standalone tool, exactly like Google Calendar. The EA types every
/// entry in herself; it owns its own data (ea_calendar_events) and never reads from or links
/// to the Meeting/Delegation/Approval/Travel/Followup business modules, which track the EA's
/// own operational work rather than the Director's schedule.
/// </summary>
public interface ICalendarService
{
    /// <summary>Lists calendar events within [From, To). From/To are required so a caller can
    /// never accidentally pull unbounded history.</summary>
    Task<List<CalendarEventDto>> GetEventsAsync(CalendarEventsQueryDto query, CancellationToken ct = default);

    Task<CalendarEventDto> CreateEventAsync(CreateCalendarEventDto dto, CancellationToken ct = default);
    Task<CalendarEventDto> GetEventAsync(long id, CancellationToken ct = default);
    Task<CalendarEventDto> UpdateEventAsync(long id, UpdateCalendarEventDto dto, CancellationToken ct = default);
    Task DeleteEventAsync(long id, CancellationToken ct = default);

    /// <summary>Counts for the "Meeting Statistics" panel: total/completed/upcoming within
    /// [From, To), plus total scheduled hours across timed (non-all-day) events.</summary>
    Task<CalendarStatsDto> GetStatsAsync(CalendarEventsQueryDto query, CancellationToken ct = default);
}
