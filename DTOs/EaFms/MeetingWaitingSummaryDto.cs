using System;

namespace Jarvis5.Dtos.EaFms
{
    public class MeetingWaitingSummaryDto
    {
    public bool IsPaused { get; set; }
    public long? CurrentPauseId { get; set; }
    public DateTime? PauseStartedAt { get; set; }
    public string? PauseReason { get; set; }

    public string? WaitingOnId { get; set; }
    public string? WaitingOnName { get; set; }
    public string? WaitingOnExternal { get; set; }

    public string? ResponseOwnerId { get; set; }
    public string? ResponseOwnerName { get; set; }
    public DateTime? ExpectedResponseAt { get; set; }

    public int PauseCount { get; set; }
    public int TotalPausedMinutes { get; set; }

    }
}

