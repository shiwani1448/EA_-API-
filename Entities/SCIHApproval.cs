namespace Jarvis5.Entities;

/// <summary>One row per Director review round. A row is created Pending by
/// Submit For Approval and then finalized in place by Approve/Reject; once
/// finalized it is never updated again — the next round is a new row.</summary>
public class SCIHApproval
{
    public long Id { get; set; }
    public long RequestId { get; set; }
    public int ApprovalRound { get; set; }
    public int ReworkCount { get; set; }

    /// <summary>Pending | Approved | Rejected — see SCIHApprovalDecision.</summary>
    public string Decision { get; set; } = string.Empty;

    public string? Comments { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>Raw JSON array of strings — improvement areas requested on rejection.</summary>
    public string ImprovementAreasJson { get; set; } = "[]";

    public long? ApprovedBy { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime CreatedDate { get; set; }
}
