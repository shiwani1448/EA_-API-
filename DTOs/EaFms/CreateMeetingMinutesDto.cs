using System;

namespace Jarvis5.Dtos.EaFms;

public class CreateMeetingMinutesDto
{
    public string? Summary { get; set; }
    public string? Notes { get; set; }
    public string? PreparedByName { get; set; }
    public DateTime? PreparedAt { get; set; }
}
