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

    /// <summary>
    /// Calculates the total paused duration that falls within [tatStart, end] using
    /// only simple pauses (non-dependency pauses). Overlapping intervals are merged.
    /// This is the canonical TAT pause calculation shared by the Meeting detail and
    /// Meeting list endpoints — do not duplicate this logic elsewhere.
    /// </summary>
    public static TimeSpan GetPausedDuration(DateTime tatStart, DateTime end, IEnumerable<WorkPause> pauses)
    {
        var intervals = pauses
            .Where(IsSimplePause)
            .Select(p => (Start: p.StartAt > tatStart ? p.StartAt : tatStart, End: (p.EndAt ?? end) < end ? p.EndAt ?? end : end))
            .Where(x => x.End > x.Start)
            .OrderBy(x => x.Start)
            .ToList();

        var total = TimeSpan.Zero;
        DateTime? currentStart = null;
        DateTime? currentEnd = null;
        foreach (var interval in intervals)
        {
            if (currentEnd is null || interval.Start > currentEnd.Value)
            {
                if (currentStart.HasValue) total += currentEnd!.Value - currentStart.Value;
                currentStart = interval.Start;
                currentEnd = interval.End;
            }
            else if (interval.End > currentEnd.Value)
            {
                currentEnd = interval.End;
            }
        }
        if (currentStart.HasValue) total += currentEnd!.Value - currentStart.Value;
        return total;
    }
}
