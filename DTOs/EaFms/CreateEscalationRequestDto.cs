namespace Jarvis5.Dtos.EaFms;

public class CreateEscalationRequestDto
{
    public long FollowupId { get; set; }
    public int EscalationLevelId { get; set; }
    public string? Notes { get; set; }
    public string? EscalatedToId { get; set; }
    public string? EscalatedToName { get; set; }
    public DateTime? NextEscalationAt { get; set; }
    public int? NextEscalationLevelId { get; set; }
    // optional acknowledgement/resolution fields are provided via dedicated endpoints
}
