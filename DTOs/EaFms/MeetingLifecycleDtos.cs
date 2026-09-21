using System.Text.Json.Serialization;

namespace Jarvis5.Dtos.EaFms;

public class MeetingStartRequestDto { }

/// <summary>
/// Request body for POST /api/ea/meetings/{meetingId}/pause.
/// PauseReason is required: blank or whitespace is rejected at the pause boundary.
/// Maximum 2000 characters (matches WorkPause.Reason varchar(2000)).
/// </summary>
public class MeetingPauseRequestDto
{
    /// <summary>
    /// The reason for pausing the Meeting task. Must be non-empty, non-whitespace,
    /// and at most 2000 characters. Stored verbatim (trimmed) as WorkPause.Reason.
    /// </summary>
    public string? PauseReason { get; set; }
}

public class MeetingResumeRequestDto { }

public class MeetingCompleteRequestDto
{
    /// <summary>Optional (the frontend decides whether it is mandatory). When supplied: trimmed, at most 4000 characters; blank is stored as null.</summary>
    [System.ComponentModel.DataAnnotations.StringLength(4000)]
    [Microsoft.AspNetCore.Mvc.FromForm(Name = "completionMom")]
    public string? CompletionMom { get; set; }
    /// <summary>Optional. When supplied it must be a valid PDF (max 25 MiB); when omitted no attachment is created.</summary>
    [Microsoft.AspNetCore.Mvc.FromForm(Name = "completionPdf")]
    public Microsoft.AspNetCore.Http.IFormFile? CompletionPdf { get; set; }
}

public class MeetingLifecycleResponseDto
{
    public string? Type { get; set; }
    public string? Subtype { get; set; }
    public List<MeetingDoerDto> Doers { get; set; } = new();
    public MeetingAssignmentSummaryDto? AssignmentSummary { get; set; }
    public long MeetingId { get; set; }    public string? StatusName { get; set; }
    public string ExecutionState { get; set; } = "InProgress";
    public bool IsPaused { get; set; }
    public DateTime? StartedAt { get; set; }
    public MeetingTatSummaryDto TatSummary { get; set; } = new();
    public DateTime? CompletedAt { get; set; }
    public DateTime? MeetingCompletedAt { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public string? CompletionMom { get; set; }
    public long? CompletionPdfAttachmentId { get; set; }
}

public class MeetingPauseResponseDto
{
    public string? Type { get; set; }
    public string? Subtype { get; set; }
    public List<MeetingDoerDto> Doers { get; set; } = new();
    public MeetingAssignmentSummaryDto? AssignmentSummary { get; set; }
    public long MeetingId { get; set; }
    public long PauseId { get; set; }
    public string? Remark { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public string? StatusName { get; set; }
    public string ExecutionState { get; set; } = "InProgress";
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
