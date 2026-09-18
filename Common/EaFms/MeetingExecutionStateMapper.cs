namespace Jarvis5.Common.EaFms;

/// <summary>
/// Canonical Meeting executionState mapping — the single source of truth reused by every
/// Meeting read/response path (MeetingService.QueryAsync, MeetingService.GetByIdAsync,
/// MeetingLifecycleService's Start/Pause/Resume/Complete responses). Previously each of
/// those computed executionState independently and disagreed (Running vs InProgress,
/// Captured vs NotStarted). Pause state is intentionally excluded here and reported
/// separately as IsPaused; a paused meeting's executionState remains InProgress.
/// </summary>
public static class MeetingExecutionStateMapper
{
    /// <param name="hasStarted">WorkflowInstance.TatStartedAt.HasValue — the authoritative,
    /// set-once execution-start marker.</param>
    /// <param name="isCompleted">WorkflowInstance.CompletedAt.HasValue (equivalently
    /// StatusName == "Completed").</param>
    public static string Map(bool hasStarted, bool isCompleted) =>
        isCompleted ? EaTaskExecutionStatus.Completed
        : hasStarted ? EaTaskExecutionStatus.InProgress
        : EaTaskExecutionStatus.NotStarted;
}
