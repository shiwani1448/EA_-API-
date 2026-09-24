namespace Jarvis5.Dtos.EaFms;

// ============================================================
// Quick Add — free text -> structured event (suggest, then EA-confirmed apply)
// ============================================================

/// <summary>Request for POST /api/ea/calendar/ai/quick-add. Free text exactly as the EA
/// typed it, e.g. "Director travel to Chennai from 25 to 26 Oct" or "Meeting with SCT team
/// tomorrow 2:30 to 5:30".</summary>
public class QuickAddCalendarEventRequestDto
{
    public string Text { get; set; } = string.Empty;
}

/// <summary>Response for POST /api/ea/calendar/ai/quick-add. A parse only — nothing is
/// written to the calendar until the EA reviews and calls quick-add/apply.</summary>
public class CalendarAiQuickAddResponseDto
{
    public string? SuggestedTitle { get; set; }
    /// <summary>ClientMeeting | InternalMeeting | Personal | Travel, or null if the text
    /// didn't give Claude enough to confidently classify it — never guessed.</summary>
    public string? SuggestedEventType { get; set; }
    public DateTime? SuggestedStartDateTime { get; set; }
    public DateTime? SuggestedEndDateTime { get; set; }
    public bool SuggestedIsAllDay { get; set; }
    public string? SuggestedLocation { get; set; }
    public string? Reasoning { get; set; }
    public string? WarningMessage { get; set; }
}

/// <summary>Request for POST /api/ea/calendar/ai/quick-add/apply. The EA's reviewed (or
/// fully retyped) fields — this never re-calls Claude, it only creates the real calendar
/// event exactly as given here, same "write exactly what the caller sent" rule the
/// Approval/Delegation AI apply endpoints already follow.</summary>
public class ApplyQuickAddSuggestionRequestDto
{
    public string? Title { get; set; }
    public string? EventType { get; set; }
    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public bool IsAllDay { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public string? OrganizerEmployeeId { get; set; }
    public string? OrganizerName { get; set; }
    public string? Notes { get; set; }
}

// ============================================================
// Conflict check — preview only, over the EA's own real calendar rows
// ============================================================

/// <summary>Request for POST /api/ea/calendar/ai/conflict-check.</summary>
public class CalendarConflictCheckRequestDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}

/// <summary>One overlapping pair, detected in plain C# from real event rows — Claude never
/// generates this list, it only narrates it in Summary.</summary>
public class CalendarAiConflictPairDto
{
    public long FirstEventId { get; set; }
    public string FirstTitle { get; set; } = string.Empty;
    public long SecondEventId { get; set; }
    public string SecondTitle { get; set; } = string.Empty;
    public string OverlapDescription { get; set; } = string.Empty;
}

/// <summary>Response for POST /api/ea/calendar/ai/conflict-check. Preview only — writes
/// nothing to any calendar event.</summary>
public class CalendarAiConflictCheckResponseDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public bool HasConflicts { get; set; }
    public List<CalendarAiConflictPairDto> Conflicts { get; set; } = new();
    public string? Summary { get; set; }
    public string? WarningMessage { get; set; }
}
