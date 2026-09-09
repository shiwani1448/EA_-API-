using System;

namespace Jarvis5.Dtos.EaFms;

public class UpdateMeetingAgendaDto
{
    public int OrderNo { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? OwnerName { get; set; }
    public bool IsReady { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? PreparedAt { get; set; }
}
