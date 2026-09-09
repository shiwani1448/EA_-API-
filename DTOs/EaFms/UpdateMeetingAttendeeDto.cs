using System;

namespace Jarvis5.Dtos.EaFms;

public class UpdateMeetingAttendeeDto
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? InviteStatus { get; set; }
    public string? AttendanceStatus { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? AttendedAt { get; set; }
    public string? Remarks { get; set; }
}
