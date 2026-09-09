using System;

namespace Jarvis5.Dtos.EaFms;

public class MeetingMinutesDto
{
    public long Id { get; set; }
    public string? Summary { get; set; }
    public string? Notes { get; set; }
    public string? PreparedByName { get; set; }
    public DateTime? PreparedAt { get; set; }
    public string? Status { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int RevisionNo { get; set; }
}
