namespace Jarvis5.Dtos.EaFms;

public class CreateMeetingDecisionDto
{
    public string? Decision { get; set; }
    public string? OwnerName { get; set; }
    public DateTime? DecisionDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Status { get; set; }
}
