namespace Jarvis5.Dtos.EaFms;

// ============================================================
// Calendar — like Google Calendar. The EA's own entries live in ea_calendar_events (editable
// here). Her work from the business modules appears automatically, read live at query time
// (never copied, so it can never go stale): Meetings on their date and time, Delegations from
// start to due date, Approvals on their required date, Travel from departure to return, and
// Follow-ups at their due time. Those entries are read-only here (IsReadOnly = true) and are
// edited in their own module (Source + SourceRecordId).
// ============================================================

public class CalendarEventDto
{
    /// <summary>ea_calendar_events.Id for the EA's own entries; 0 for module entries (use EventKey).</summary>
    public long Id { get; set; }
    /// <summary>Unique key for every entry, e.g. "Calendar:12", "Meeting:20", "Delegation:7".</summary>
    public string EventKey { get; set; } = string.Empty;
    /// <summary>Calendar | Meeting | Delegation | Approval | Travel | Follow-up.</summary>
    public string Source { get; set; } = "Calendar";
    /// <summary>The module record id (MeetingId, DelegationId, …); null for the EA's own entries.</summary>
    public long? SourceRecordId { get; set; }
    public string? ReferenceNo { get; set; }
    /// <summary>true for module entries: edit them in their own module, not in the calendar.</summary>
    public bool IsReadOnly { get; set; }
    /// <summary>The module's own status (e.g. Pending, InProgress, Approved); null for the EA's own entries.</summary>
    public string? Status { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>ClientMeeting | InternalMeeting | Personal | Travel | Task (Task = Delegation/Approval/Follow-up).</summary>
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
    /// <summary>Optional whitelist: ClientMeeting, InternalMeeting, Personal, Travel, Task.
    /// Null/empty means all of them.</summary>
    public string[]? EventTypes { get; set; }
    /// <summary>Optional whitelist of sources: Calendar, Meeting, Delegation, Approval, Travel, Follow-up. Null/empty = all.</summary>
    public string[]? Sources { get; set; }
    /// <summary>false = only the EA's own calendar entries (the old standalone view). Default true.</summary>
    public bool IncludeLinked { get; set; } = true;
    /// <summary>Whose calendar. Defaults to the logged-in EA for module entries; when sent, her own entries are
    /// limited to ones she created/organises too. Leave empty (and no login) to see everyone.</summary>
    public string? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
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
