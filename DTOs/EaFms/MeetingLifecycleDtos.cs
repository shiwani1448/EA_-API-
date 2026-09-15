using System.Text.Json.Serialization;

namespace Jarvis5.Dtos.EaFms;

public class MeetingStartRequestDto { }
public class MeetingPauseRequestDto { }
public class MeetingResumeRequestDto { }

public class MeetingCompleteRequestDto
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(4000, MinimumLength = 1)]
    [Microsoft.AspNetCore.Mvc.FromForm(Name = "completionMom")]
    public string CompletionMom { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required]
    [Microsoft.AspNetCore.Mvc.FromForm(Name = "completionPdf")]
    public Microsoft.AspNetCore.Http.IFormFile CompletionPdf { get; set; } = null!;
}

public class MeetingLifecycleResponseDto
{
    public string? Type { get; set; }
    public string? Subtype { get; set; }
    public List<MeetingDoerDto> Doers { get; set; } = new();
    public MeetingAssignmentSummaryDto? AssignmentSummary { get; set; }
    public long MeetingId { get; set; }    public string? StatusName { get; set; }
    public string ExecutionState { get; set; } = "Running";
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
