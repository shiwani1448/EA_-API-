using System;

namespace Jarvis5.Dtos.EaFms;

public class MeetingListItemResponseDto
{
    public long MeetingId { get; set; }
    public string? MeetingNumber { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Purpose { get; set; }

    // Stored task TAT snapshot (null for historical Meetings without a task)
    public long ModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public int? TatMinutes { get; set; }

    public string? Type { get; set; }
    public string? Subtype { get; set; }
    public List<MeetingDoerDto> Doers { get; set; } = new();

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

    public string ExecutionState { get; set; } = "NotStarted";
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

    /// <summary>
    /// Full-precision TAT summary, computed via the exact same calculation path as
    /// GetByIdAsync's dto.TatSummary (WorkPauseClassifier.GetPausedDuration, same
    /// start/end/paused/used formula). Added so the register table's initial values
    /// already match what View Details shows, instead of the row visibly "correcting
    /// itself" once the whole-minute list snapshot is replaced by the detail response.
    /// Never null: mirrors GetByIdAsync's own guaranteed-non-null "unavailable" fallback
    /// (Tat: null, TotalTat/PauseTime: zero, PauseCount: 0) when there is no workflow, no
    /// task snapshot, or no configured TAT.
    /// </summary>
    public MeetingTatSummaryDto TatSummary { get; set; } = new();

    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
