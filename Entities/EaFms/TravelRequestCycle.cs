namespace Jarvis5.Entities.EaFms;

/// <summary>One submitted/reviewed round for a TravelRequest.</summary>
public class TravelRequestCycle
{
    public long Id { get; set; }
    public long TravelRequestId { get; set; }
    public TravelRequest TravelRequest { get; set; } = null!;
    public int CycleNo { get; set; }
    public string? SubmittedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ApproverId { get; set; }
    public string? ApproverNameSnapshot { get; set; }
    public string? DecisionState { get; set; }
    public string? ChangeReason { get; set; }
    public string? ChangesMade { get; set; }
    public string? DecisionComment { get; set; }
    public string? DecisionBy { get; set; }
    public DateTime? DecisionAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
