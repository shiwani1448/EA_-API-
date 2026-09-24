namespace Jarvis5.Dtos.EaFms;

// ============================================================
// Standalone calendar — exactly like Google Calendar. The EA types every entry in herself;
// this module owns its own data (ea_calendar_events) and never reads from or links to the
// Meeting/Delegation/Approval/Travel/Followup business modules, which track the EA's own
// operational work rather than the Director's schedule.
// ============================================================

public class CalendarEventDto
{
    public long Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>ClientMeeting | InternalMeeting | Personal | Travel — see CalendarEventType.</summary>
    public string EventType { get; set; } = string.Empty;

    public DateTime StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public bool IsAllDay { get; set; }

    public string? Location { get; set; }
    public string? Priority { get; set; }

    public string? OrganizerEmployeeId { get; set; }
    public string? OrganizerName { get; set; }
    public string? Notes { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

/// <summary>Query for GET /api/ea/calendar/events. From/To are required so a caller can
/// never accidentally pull the whole history.</summary>
public class CalendarEventsQueryDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    /// <summary>Optional whitelist: ClientMeeting, InternalMeeting, Personal, Travel.
    /// Null/empty means all of them.</summary>
    public string[]? EventTypes { get; set; }
    public string? OrganizerEmployeeId { get; set; }
    public string? Priority { get; set; }
    /// <summary>When false, completed events are left out (the "Show Completed" filter
    /// toggle). Defaults to true so existing callers keep seeing everything.</summary>
    public bool IncludeCompleted { get; set; } = true;
}

/// <summary>Counts backing the frontend's "Meeting Statistics" panel.</summary>
public class CalendarStatsDto
{
    public int Total { get; set; }
    public int Completed { get; set; }
    public int Upcoming { get; set; }
    public double ScheduledHours { get; set; }
}

// ============================================================
// Calendar event CRUD
// ============================================================

public class CreateCalendarEventDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    /// <summary>ClientMeeting | InternalMeeting | Personal | Travel. Required.</summary>
    public string? EventType { get; set; }
    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public bool IsAllDay { get; set; }
    public string? Location { get; set; }
    public string? Priority { get; set; }
    public string? OrganizerEmployeeId { get; set; }
    public string? OrganizerName { get; set; }
    public string? Notes { get; set; }
}

public class UpdateCalendarEventDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? EventType { get; set; }
    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public bool IsAllDay { get; set; }
    public string? Location { get; set; }
    public string? Priority { get; set; }
    public string? OrganizerEmployeeId { get; set; }
    public string? OrganizerName { get; set; }
    public string? Notes { get; set; }
    public bool IsCompleted { get; set; }
}
