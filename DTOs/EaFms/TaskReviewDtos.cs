namespace Jarvis5.Dtos.EaFms;

// ============================================================
// REQUEST DTOs — shared across Delegation/Approval
// ============================================================

/// <summary>
/// Reviewer/submitter identity is frontend-supplied snapshot data only — never resolved
/// via HRMS/Employees/Users/JWT. Ordinary business fields stay optional from the backend
/// where technically possible (project-wide requiredness rule); the state-machine
/// preconditions (no current PendingReview cycle, EaTask not Cancelled/Completed) are
/// enforced regardless of what is supplied here.
/// </summary>
public class SubmitForReviewRequestDto
{
    public string? ReviewerId { get; set; }
    public string? ReviewerName { get; set; }
    public string? SubmittedById { get; set; }
    public string? SubmittedByName { get; set; }
}

public class ApproveTaskReviewRequestDto
{
    public string? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public string? ReviewRemark { get; set; }
}

public class RequestTaskReworkRequestDto
{
    public string? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public string? ReworkRemark { get; set; }
}

// ============================================================
// RESPONSE DTOs
// ============================================================

/// <summary>
/// One shared shape reused verbatim across every task-bearing module's response DTO —
/// never independently re-declared per module. Null/"no review yet" representation:
/// Status is null, ReviewCycleNumber is 0, every other field is null.
/// </summary>
public class TaskReviewSummaryDto
{
    /// <summary>PendingReview | Approved | ReworkRequested | null (never submitted for review yet).</summary>
    public string? Status { get; set; }
    public int ReviewCycleNumber { get; set; }
    public string? ReviewerId { get; set; }
    public string? ReviewerName { get; set; }
    public string? SubmittedById { get; set; }
    public string? SubmittedByName { get; set; }
    public DateTime? SubmittedForReviewAt { get; set; }
    public string? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewRemark { get; set; }
    public string? ReworkRemark { get; set; }
}

/// <summary>One row of review-cycle history, oldest (CycleNo 1) first. EaTask-based — WorkflowInstanceId is never exposed.</summary>
public class TaskReviewHistoryItemDto
{
    public int ReviewCycleNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ReviewerId { get; set; }
    public string? ReviewerName { get; set; }
    public string? SubmittedById { get; set; }
    public string? SubmittedByName { get; set; }
    public DateTime SubmittedForReviewAt { get; set; }
    public string? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewRemark { get; set; }
    public string? ReworkRemark { get; set; }
}
