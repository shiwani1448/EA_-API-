using Jarvis5.Entities.EaFms;

namespace Jarvis5.Common.EaFms;

/// <summary>
/// Deterministic Simple Pause vs Dependency Waiting discrimination for ea_work_pauses.
/// Waiting writers always persist at least one WaitingOn* field (enforced by validators
/// and WaitAsync). Simple Pause writers leave all dependency markers null.
/// </summary>
public static class WorkPauseClassifier
{
    public static bool HasWaitingDependency(string? waitingOnId, string? waitingOnName, string? waitingOnExternal) =>
        !string.IsNullOrWhiteSpace(waitingOnId)
        || !string.IsNullOrWhiteSpace(waitingOnName)
        || !string.IsNullOrWhiteSpace(waitingOnExternal);

    public static bool IsDependencyWaiting(WorkPause pause) =>
        pause.FollowupId.HasValue
        || pause.RequestSentAt.HasValue
        || pause.ExpectedResponseAt.HasValue
        || HasWaitingDependency(pause.WaitingOnId, pause.WaitingOnName, pause.WaitingOnExternal);

    public static bool IsSimplePause(WorkPause pause) => !IsDependencyWaiting(pause);
}
