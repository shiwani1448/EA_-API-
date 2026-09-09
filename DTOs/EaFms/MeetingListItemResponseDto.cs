using System;

namespace Jarvis5.Dtos.EaFms;

public class MeetingListItemResponseDto
{
    public long MeetingId { get; set; }
    public string? MeetingNumber { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Purpose { get; set; }

    public string? MeetingType { get; set; }
    public string? Category { get; set; }

    public DateTime? MeetingDate { get; set; }
    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }

    public string? Location { get; set; }
    public string? MeetingMode { get; set; }

    public string? OrganizerId { get; set; }
    public string? OrganizerName { get; set; }


    public string? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }

    public string? Priority { get; set; }

    public string? StatusName { get; set; }


    public bool IsConfidential { get; set; }

    public string ExecutionState { get; set; } = "Captured";
    public bool IsPaused { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsOverdue { get; set; }

    public DateTime? LastFollowupAt { get; set; }
    public DateTime? NextFollowupAt { get; set; }
    public int? OpenFollowupCount { get; set; }

    public int? CurrentEscalationLevelNumber { get; set; }
    public int? OpenEscalationCount { get; set; }

    public bool AgendaReady { get; set; }
    public string? MinutesStatus { get; set; }

    public int? OpenActionCount { get; set; }
    public int? OverdueActionCount { get; set; }

    public int? TatUsedMinutes { get; set; }
    public int? TatPausedMinutes { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
