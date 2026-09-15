using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
namespace Jarvis5.Common.EaFms;
public static class MeetingTiming
{
    public static MeetingTatSummaryDto Calculate(WorkflowInstance wf, IEnumerable<WorkPause> history, int? minutes, DateTime now)
    {
        var pauses = history.Where(WorkPauseClassifier.IsSimplePause).OrderBy(p => p.StartAt).ToList();
        var end = wf.CompletedAt ?? now;
        var paused = TimeSpan.Zero;
        var elapsed = TimeSpan.Zero;
        if (wf.TatStartedAt is DateTime start)
        {
            elapsed = end > start ? end - start : TimeSpan.Zero;
            DateTime? mergedEnd = null;
            foreach (var p in pauses)
            {
                var a = p.StartAt > start ? p.StartAt : start;
                var b = (p.EndAt ?? end) < end ? (p.EndAt ?? end) : end;
                if (mergedEnd.HasValue && a < mergedEnd) a = mergedEnd.Value;
                if (b > a) { paused += b - a; mergedEnd = b; }
            }
        }
        var used = elapsed - paused;
        var tat = minutes.HasValue ? TimeSpan.FromMinutes(minutes.Value) : (TimeSpan?)null;
        return new MeetingTatSummaryDto
        {
            Tat = tat, TotalTat = used,
            StartTime = wf.TatStartedAt, EndTime = wf.CompletedAt,
            LastActiveTime = wf.CompletedAt ?? pauses.LastOrDefault()?.EndAt ?? pauses.LastOrDefault()?.StartAt ?? wf.TatStartedAt,
            PauseTime = paused, PauseCount = pauses.Count
        };
    }
}
