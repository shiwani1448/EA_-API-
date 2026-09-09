using System;

namespace Jarvis5.Entities.EaFms;

public class MeetingAttendee
{
    public long Id { get; set; }
    public long MeetingId { get; set; }
    public Meeting? Meeting { get; set; }

    public string? ParticipantId { get; set; }
    public string? ParticipantName { get; set; }
    public string? Email { get; set; }
    public string? Organization { get; set; }
    public string? Role { get; set; }

    public string? InvitationStatus { get; set; }
    public string? AttendanceStatus { get; set; }

    public DateTime? InvitedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? AttendedAt { get; set; }

    public string? Remarks { get; set; }

    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
}
