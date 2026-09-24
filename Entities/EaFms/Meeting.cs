using System;

namespace Jarvis5.Entities.EaFms;

public class Meeting
{
    public string? DelegationDecision { get; set; }
    public DateTime? DelegationDecidedAt { get; set; }
    public string? DelegationDecidedBy { get; set; }

    public long Id { get; set; }

    // Business reference
    public string? MeetingNumber { get; set; }

    public long? IntakeRequestId { get; set; }
    public IntakeRequest? IntakeRequest { get; set; }

    public long? WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }

    // Core fields
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Purpose { get; set; }

    public string? MeetingType { get; set; }
    public string? Category { get; set; }

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
    public int? StatusId { get; set; }

    public DateTime? RequiredDate { get; set; }
    public DateTime? AgendaDueAt { get; set; }
    public DateTime? MinutesDueAt { get; set; }

    public bool IsConfidential { get; set; }

    public string[] DoerIds { get; set; } = Array.Empty<string>();
    public string[] DoerNames { get; set; } = Array.Empty<string>();
    public string? CompletionMom { get; set; }
    public long? CompletionPdfAttachmentId { get; set; }
    public Attachment? CompletionPdfAttachment { get; set; }

    public DateTime? CompletedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public bool IsDeleted { get; set; }
}
