using System;

namespace Jarvis5.Dtos.EaFms;

public class UpdateMeetingRequestDto
{
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

    public long? IntakeRequestId { get; set; }
}
