using System;

namespace Jarvis5.Dtos.EaFms;

public class MeetingDetailResponseDto
{
    // Core
    public long MeetingId { get; set; }    public string? MeetingNumber { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Purpose { get; set; }

    // Stored task TAT snapshot (null for historical Meetings without a task)
    public long ModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public int? TatMinutes { get; set; }

    // Frontend task/TAT binding (task/description from Meeting; allotted from ea_tasks snapshot when present)
    public string? Task { get; set; }
    public int? AllottedTatMinutes { get; set; }
    public long? EaTaskId { get; set; }

    public string? Type { get; set; }
    public string? Subtype { get; set; }
    public List<MeetingDoerDto> Doers { get; set; } = new();

    public string? Source { get; set; }
    public string? SourceChannel { get; set; }
    public string? SourceReferenceId { get; set; }

    public DateTime? MeetingDate { get; set; }
    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }

    public string? Location { get; set; }
    public string? MeetingMode { get; set; }
    public string? MeetingLink { get; set; }

    public string? OrganizerId { get; set; }
    public string? OrganizerName { get; set; }

    public string? Priority { get; set; }

    public string? StatusName { get; set; }
    public string ExecutionState { get; set; } = "NotStarted";
    public bool IsPaused { get; set; }


    public DateTime? RequiredDate { get; set; }
    public DateTime? AgendaDueAt { get; set; }
    public DateTime? MinutesDueAt { get; set; }

    public bool IsConfidential { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletionMom { get; set; }
    public long? CompletionPdfAttachmentId { get; set; }
    public DateTime? ArchivedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public string? CreatedByName { get; set; }
    public DateTime CreatedDate { get; set; }

    public string? ModifiedBy { get; set; }
    public string? ModifiedByName { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public bool IsDeleted { get; set; }

    // Summaries (populated by services)
    public MeetingAssignmentSummaryDto? AssignmentSummary { get; set; }
    public MeetingWaitingSummaryDto? WaitingSummary { get; set; }
    public MeetingTatSummaryDto? TatSummary { get; set; }

    public MeetingFollowupSummaryDto? FollowupSummary { get; set; }
    public object? EscalationSummary { get; set; }
    public object? RevisionSummary { get; set; }

    public object? AgendaSummary { get; set; }
    public object? AttendeeSummary { get; set; }
    public object? MinutesSummary { get; set; }
    public object? DecisionSummary { get; set; }
    public object? ActionSummary { get; set; }
    public object? AttachmentSummary { get; set; }
}
