using Jarvis5.Common.EaFms;

namespace Jarvis5.Entities.EaFms;

/// <summary>
/// A standalone calendar entry — exactly like a Google Calendar event. The EA types every
/// entry in herself (a Director's meeting, a travel block, personal time); nothing here is
/// ever read from or linked to the Meeting/Delegation/Approval/Travel business modules —
/// those track the EA's own operational work, not the Director's calendar.
///
/// OrganizerEmployeeId/OrganizerName are a frontend-supplied actor snapshot, the same
/// convention Meeting.OrganizerId/OrganizerName and Followup.DoerId/DoerName already use
/// throughout this codebase — never an HRMS/Users FK lookup.
/// </summary>
public class CalendarEvent
{
    public long Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>ClientMeeting | InternalMeeting | Personal | Travel — see CalendarEventType.
    /// Matches the EA's Calendar filter checkboxes exactly.</summary>
    public string EventType { get; set; } = CalendarEventType.InternalMeeting;

    public DateTime StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public bool IsAllDay { get; set; }

    public string? Location { get; set; }
    public string? Priority { get; set; }

    public string? OrganizerEmployeeId { get; set; }
    public string? OrganizerName { get; set; }
    public string? Notes { get; set; }

    /// <summary>The EA's own "done" marker — drives the "Show Completed" filter toggle.
    /// Never derived from anything else; the EA sets it explicitly.</summary>
    public bool IsCompleted { get; set; }

    public bool IsDeleted { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
