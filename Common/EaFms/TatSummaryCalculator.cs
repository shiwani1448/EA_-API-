using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;

namespace Jarvis5.Common.EaFms;

/// <summary>
/// The one canonical elapsed-minus-paused TAT summary formula for a TAT-configured business record.
/// Shared verbatim by Meeting (anchored on WorkflowInstance.TatStartedAt/CompletedAt) and Delegation
/// (anchored on the central EaTask.StartedAt/CompletedAt) so both compute tat/totalTat/tatDifference/
/// startTime/endTime/lastActiveTime/pauseTime/pauseCount identically instead of duplicating the formula
/// per caller. Callers must already know AllottedTatMinutes is configured — the no-TAT/"unavailable"
/// shape is each caller's own concern (Meeting never has a no-TAT task; Delegation's no-TAT case is a
/// distinct, first-class scenario handled by its own caller code).
/// </summary>
public static class TatSummaryCalculator
{
    public static MeetingTatSummaryDto Calculate(
        int allottedTatMinutes, DateTime? start, DateTime? completedAt, IReadOnlyCollection<WorkPause> pauses, DateTime now)
    {
        var totalTat = TimeSpan.FromMinutes(allottedTatMinutes);
        var end = completedAt ?? now;
        var paused = start.HasValue ? WorkPauseClassifier.GetPausedDuration(start.Value, end, pauses) : TimeSpan.Zero;
        var used = start.HasValue ? end - start.Value - paused : TimeSpan.Zero;
        if (used < TimeSpan.Zero) used = TimeSpan.Zero;

        var openSimplePause = pauses.FirstOrDefault(p => p.EndAt == null && WorkPauseClassifier.IsSimplePause(p));

        return new MeetingTatSummaryDto
        {
            Tat = used,
            TotalTat = totalTat,
            TatDifference = totalTat - used,
            StartTime = start,
            EndTime = completedAt,
            LastActiveTime = completedAt ?? (openSimplePause?.StartAt ?? end),
            PauseTime = paused,
            PauseCount = pauses.Count(WorkPauseClassifier.IsSimplePause)
        };
    }
}
