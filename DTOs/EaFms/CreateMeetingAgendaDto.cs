using System;

namespace Jarvis5.Dtos.EaFms;

public class CreateMeetingAgendaDto
{
    public int OrderNo { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? OwnerName { get; set; }
    public bool IsReady { get; set; }
    public DateTime? DueAt { get; set; }
}
