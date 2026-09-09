using System;

namespace Jarvis5.Dtos.EaFms;

public class MeetingFollowupSummaryDto
{
    public int TotalFollowups { get; set; }
    public int OpenFollowups { get; set; }
    public int CompletedFollowups { get; set; }
    public int OverdueFollowups { get; set; }

    public DateTime? LastFollowupAt { get; set; }
    public DateTime? NextFollowupAt { get; set; }

    public string? WaitingOnId { get; set; }
    public string? WaitingOnName { get; set; }
    public string? WaitingOnExternal { get; set; }

    public string? ResponseOwnerId { get; set; }
    public string? ResponseOwnerName { get; set; }
    public DateTime? ExpectedResponseAt { get; set; }

    public bool HasOverdueFollowup { get; set; }
}
