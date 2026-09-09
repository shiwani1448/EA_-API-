using System;

namespace Jarvis5.Dtos.EaFms;

public class MeetingDecisionDto
{
    public long Id { get; set; }
    public string? Decision { get; set; }
    public string? OwnerName { get; set; }
    public DateTime? DecisionDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Status { get; set; }
}
