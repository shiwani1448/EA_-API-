using System.Text.Json.Serialization;

namespace Jarvis5.Dtos.EaFms;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class MeetingStartRequestDto
{
    public string? Notes { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class MeetingPauseRequestDto
{
    public string Remark { get; set; } = string.Empty;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class MeetingResumeRequestDto
{
    public string? Remark { get; set; }
}

/// <summary>
/// Meeting business completion contract. Only fields supported by the current Meeting model.
/// Minutes/decisions/actions remain on their dedicated Meeting child APIs — not invented here.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class MeetingCompleteRequestDto
{
    public string Notes { get; set; } = string.Empty;
}

public class MeetingLifecycleResponseDto
{
    public long MeetingId { get; set; }    public string? StatusName { get; set; }
    public string ExecutionState { get; set; } = "Running";
    public bool IsPaused { get; set; }
    public DateTime? StartedAt { get; set; }
    public MeetingTatSummaryDto TatSummary { get; set; } = new();
    public DateTime? CompletedAt { get; set; }
    public DateTime? MeetingCompletedAt { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class MeetingPauseResponseDto
{
    public long MeetingId { get; set; }
    public long PauseId { get; set; }
    public string? Remark { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public string? StatusName { get; set; }
    public string ExecutionState { get; set; } = "Running";
    public bool IsPaused { get; set; }
    public DateTime? StartedAt { get; set; }
    public MeetingTatSummaryDto TatSummary { get; set; } = new();
    public DateTime? CompletedAt { get; set; }
    public bool IsActive { get; set; }
}

public class MeetingWaitingResponseDto
{
    public long MeetingId { get; set; }
    public WorkflowWaitingResponseDto Waiting { get; set; } = null!;
}

public class MeetingWaitingListResponseDto
{
    public long MeetingId { get; set; }
    public WorkflowWaitingListResponseDto Waiting { get; set; } = null!;
}
