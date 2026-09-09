namespace Jarvis5.Dtos.Approval;

/// <summary>Shape of a single SCIH_Approval row, as returned in the approval
/// detail screen (current round) and the rounds history list.</summary>
public class ApprovalRoundDto
{
    public long Id { get; set; }
    public int ApprovalRound { get; set; }
    public int ReworkCount { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public string? RejectionReason { get; set; }
    public List<string> ImprovementAreas { get; set; } = new();
    public long? ApprovedBy { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime CreatedDate { get; set; }
}
