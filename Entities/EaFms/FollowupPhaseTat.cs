namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One row per Followup "phase" — mirrors DelegationPhaseTat, but Followup has no Review/Rework
/// cycle: there is exactly one phase, TaskType always "Actual", ReviewCycleNumber always 0.
/// StartedAt to EndedAt is the phase's own TAT window (EndedAt null while it is the current,
/// still-open phase), resolved against the Followup's own (Type, "Actual") TAT Rule.
/// Used/Paused/PauseCount are frozen (written once) only when the phase closes (Complete);
/// while open they are computed live from StartedAt and the Followup's shared WorkPauses, the
/// same way DelegationPhaseTat's open phase is computed live.
///
/// Unlike DelegationPhaseTat, this also stores who started/ended the phase directly (
/// StartedById/StartedByName/EndedById/EndedByName) rather than relying solely on the
/// AuditLog/EaTaskHistoryBuilder reconstruction — an explicit ask for Follow-up's own phase
/// history view.
/// </summary>
public class FollowupPhaseTat
{
    public long Id { get; set; }
    public long FollowupId { get; set; }
    public Followup Followup { get; set; } = null!;

    /// <summary>Always "Actual" — see DelegationTaskType.Actual (reused, not forked).</summary>
    public string TaskType { get; set; } = null!;
    /// <summary>Always 0 — Followup has no review cycle.</summary>
    public int ReviewCycleNumber { get; set; }

    public DateTime StartedAt { get; set; }
    /// <summary>Null while this is the current, still-open phase.</summary>
    public DateTime? EndedAt { get; set; }

    public string? StartedById { get; set; }
    public string? StartedByName { get; set; }
    public string? EndedById { get; set; }
    public string? EndedByName { get; set; }

    /// <summary>Null when no TAT rule is configured for (Type, "Actual") — "no TAT" for this Followup.</summary>
    public int? AllottedTatMinutes { get; set; }
    public long? TatRuleId { get; set; }

    /// <summary>Frozen once EndedAt is set; null while open (callers compute the live value instead).</summary>
    public int? TatUsedMinutes { get; set; }
    public int? TatPausedMinutes { get; set; }
    public int? PauseCount { get; set; }

    /// <summary>Exact decimal seconds frozen on closure; null for open phases.</summary>
    public decimal? TatUsedSeconds { get; set; }
    public decimal? TatPausedSeconds { get; set; }

    public string CreatedBy { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
