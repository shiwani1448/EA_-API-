namespace Jarvis5.Common.EaFms;

/// <summary>
/// Canonical Task Review vocabulary persisted on TaskReview.ReviewStatus. Completely
/// separate from EaTaskExecutionStatus (NotStarted/InProgress/Completed/Cancelled) and
/// from any module's own business status (Approval.WorkflowStatus, Travel.ApprovalState).
/// </summary>
public static class TaskReviewStatus
{
    public const string PendingReview = "PendingReview";
    public const string Approved = "Approved";
    public const string ReworkRequested = "ReworkRequested";

    private static readonly HashSet<string> All = new(StringComparer.Ordinal)
    { PendingReview, Approved, ReworkRequested };

    public static bool IsValid(string value) => All.Contains(value);
    public static IReadOnlyCollection<string> Values => All;
}
