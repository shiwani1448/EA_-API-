namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One AI escalation-level recommendation for a Followup, shaped like
/// FollowupAiEscalationSuggestionResponseDto — same "one table per real AI task" convention.
/// AppliedEscalationId/IsApplied/AppliedAt track which real ea_escalations row actually got
/// created via EscalationService.CreateAsync, once the EA confirmed (or overrode) the level —
/// null/false when she never applied it, or created an Escalation manually without asking
/// for a suggestion first.
/// </summary>
public class FollowupEscalationSuggestion
{
    public long Id { get; set; }
    public long FollowupId { get; set; }

    public int? RecommendedEscalationLevelId { get; set; }
    public string? RecommendedEscalationLevelName { get; set; }
    public string? Reasoning { get; set; }
    public string? WarningMessage { get; set; }

    public bool IsApplied { get; set; }
    public DateTime? AppliedAt { get; set; }
    public long? AppliedEscalationId { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
