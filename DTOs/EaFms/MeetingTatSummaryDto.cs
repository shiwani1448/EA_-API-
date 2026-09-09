namespace Jarvis5.Dtos.EaFms;

/// <summary>Single Meeting timing contract. All durations are emitted as TimeSpan values.</summary>
public class MeetingTatSummaryDto
{
    public TimeSpan? Tat { get; set; }
    public TimeSpan TotalTat { get; set; }
    public TimeSpan TatDifference { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public DateTime? LastActiveTime { get; set; }
    public TimeSpan PauseTime { get; set; }
    public int PauseCount { get; set; }
}