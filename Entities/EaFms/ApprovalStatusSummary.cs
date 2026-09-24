namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One AI status summary for an Approval Request, shaped exactly like
/// ApprovalAiStatusSummaryResponseDto — every field is scalar, no jsonb needed. Purely
/// advisory: there is no confirm/apply step for a status narrative in the real flow.
/// </summary>
public class ApprovalStatusSummary
{
    public long Id { get; set; }
    public long ApprovalRequestId { get; set; }

    public string? Summary { get; set; }
    public string? WorkflowStatus { get; set; }
    public int CurrentCycleNo { get; set; }
    public string? DueState { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
