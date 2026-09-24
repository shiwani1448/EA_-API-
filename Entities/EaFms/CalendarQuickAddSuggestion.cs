namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One AI quick-add parse of free text (e.g. "Director travel to Chennai from 25 to 26 Oct")
/// into structured calendar-event fields — same "one table per real AI task, real typed
/// columns" convention every other AI task table in this codebase uses. AppliedCalendarEventId/
/// IsApplied/AppliedAt track whether, and into which real ea_calendar_events row, the EA
/// actually confirmed this suggestion — null/false when she edited the fields and created the
/// event manually instead, or never applied it at all.
/// </summary>
public class CalendarQuickAddSuggestion
{
    public long Id { get; set; }

    /// <summary>The EA's original free-text input, verbatim.</summary>
    public string InputText { get; set; } = string.Empty;

    public string? SuggestedTitle { get; set; }
    /// <summary>ClientMeeting | InternalMeeting | Personal | Travel, or null if Claude
    /// could not confidently classify the text.</summary>
    public string? SuggestedEventType { get; set; }
    public DateTime? SuggestedStartDateTime { get; set; }
    public DateTime? SuggestedEndDateTime { get; set; }
    public bool SuggestedIsAllDay { get; set; }
    public string? SuggestedLocation { get; set; }
    public string? Reasoning { get; set; }
    public string? WarningMessage { get; set; }

    public bool IsApplied { get; set; }
    public DateTime? AppliedAt { get; set; }
    /// <summary>The real ea_calendar_events row actually created, once confirmed.</summary>
    public long? AppliedCalendarEventId { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
