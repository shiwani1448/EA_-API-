namespace Jarvis5.Entities.EaFms;

public class ApprovalCycle
{
    public long Id { get; set; }
    public long ApprovalRequestId { get; set; }
    public ApprovalRequest ApprovalRequest { get; set; } = null!;
    // TaskId is derived from ApprovalRequest.EaTaskId; do not duplicate GUID-based TaskId here.
    public int CycleNo { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? SubmittedBy { get; set; }
    public DateTime? RequiredApprovalDate { get; set; }
    public string? ApproverId { get; set; }
    public string? Status { get; set; }
    public string? ChangeReason { get; set; }
    public string? DecisionComment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
