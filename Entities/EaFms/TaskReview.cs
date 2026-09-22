namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One row per Doer-submits-for-review / Reviewer-decides round for a central EaTask.
/// Append-only: a cycle is never overwritten across resubmission — ReworkRequested is
/// finalized in place, then the NEXT SubmitForReview inserts a new row with CycleNo+1.
/// Anchored on EaTaskId (not WorkflowInstanceId) so it works uniformly across Delegation and Approval. Historical cycles for other modules remain stored.
/// Separate from EaTask.ExecutionStatus (NotStarted/InProgress/Completed/Cancelled) and
/// from any module's own business status (Approval.WorkflowStatus, Travel.ApprovalState) —
/// Phase 1 does not gate or trigger either of those from review state.
/// </summary>
public class TaskReview
{
    public long Id { get; set; }

    public long EaTaskId { get; set; }
    public EaTask? EaTask { get; set; }

    public int ReviewCycleNo { get; set; }

    /// <summary>PendingReview | Approved | ReworkRequested (Jarvis5.Common.EaFms.TaskReviewStatus).</summary>
    public string ReviewStatus { get; set; } = null!;

    /// <summary>Frontend-supplied reviewer snapshot. Never resolved via HRMS/Employees/Users/JWT.</summary>
    public string? ReviewerId { get; set; }
    public string? ReviewerName { get; set; }

    /// <summary>Frontend-supplied submitter snapshot for this cycle.</summary>
    public string? SubmittedById { get; set; }
    public string? SubmittedByName { get; set; }
    public DateTime SubmittedAt { get; set; }

    /// <summary>Frontend-supplied decision-actor snapshot for this cycle's Approve/Rework decision.</summary>
    public string? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public string? ReviewRemark { get; set; }
    public string? ReworkRemark { get; set; }

    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
