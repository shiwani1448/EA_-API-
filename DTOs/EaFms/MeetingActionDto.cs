using System;

namespace Jarvis5.Dtos.EaFms;

public class MeetingActionDto
{
    public long Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? OwnerName { get; set; }
    public int? PriorityLevelId { get; set; }
    public string? PriorityLevelName { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Status { get; set; }
    public bool IsOverdue { get; set; }
}
