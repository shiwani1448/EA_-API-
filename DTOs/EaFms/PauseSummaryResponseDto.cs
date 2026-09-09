using System;

namespace Jarvis5.Dtos.EaFms;

public class PauseSummaryResponseDto
{
    public long WorkflowInstanceId { get; set; }
    public bool IsPaused { get; set; }
    public int PauseCount { get; set; }
    public int TotalPausedMinutes { get; set; }
    public long? CurrentPauseId { get; set; }
    public DateTime? CurrentPauseStartedAt { get; set; }
    public string? CurrentPauseReason { get; set; }
    public string? WaitingOnId { get; set; }
    public string? WaitingOnName { get; set; }
    public string? WaitingOnExternal { get; set; }
    public string? ResponseOwnerId { get; set; }
    public string? ResponseOwnerName { get; set; }
    public DateTime? ExpectedResponseAt { get; set; }
    public DateTime? RequestSentAt { get; set; }
    public int? TatPausedMinutes { get; set; }
    public int? TatUsedMinutes { get; set; }
}
