namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One row per Approval "phase": the Actual working window (Actual, ReviewCycleNumber 0), or a
/// Review/Rework pair per Task Review cycle (ReviewCycleNumber matches the ea_task_reviews cycle it
/// belongs to). Each phase has its own TAT window — StartedAt to EndedAt, EndedAt null while it is
/// the current, still-open phase — and its own Allotted/Used/Paused/PauseCount snapshot, resolved
/// against the Approval's own (RequestType, TaskType) TAT Rule. Used/Paused/PauseCount are frozen
/// (written once) only when the phase closes; while open they are computed live from StartedAt and
/// the Approval's shared WorkPauses, the same way DelegationPhaseTat already does. Entirely separate
/// from the shared ea_task_reviews table — TaskReviewService's own engine never touches this, exactly
/// like the review/rework attachment mechanism. Structurally identical to DelegationPhaseTat, keyed
/// on ApprovalRequestId instead of DelegationId.
/// </summary>
public class ApprovalPhaseTat
{
    public long Id { get; set; }
    public long ApprovalRequestId { get; set; }
    public ApprovalRequest ApprovalRequest { get; set; } = null!;

    /// <summary>Actual | Review | Rework — see DelegationTaskType (reused verbatim for Approval).</summary>
    public string TaskType { get; set; } = null!;
    /// <summary>0 for Actual. For Review/Rework, the review cycle number this phase belongs to.</summary>
    public int ReviewCycleNumber { get; set; }

    /// <summary>
    /// Null while this phase exists but its TAT clock has not been started yet — every phase
    /// (Actual/Review/Rework alike) is opened this way; see ApprovalLifecycleService.OpenPhaseAsync/
    /// StartActualAsync/StartReviewAsync/StartReworkAsync. Set once, the moment the phase's own
    /// explicit Start action runs.
    /// </summary>
    public DateTime? StartedAt { get; set; }
    /// <summary>Null while this is the current, still-open phase.</summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>Null when no TAT rule is configured for (RequestType, TaskType) — "no TAT" for this phase specifically.</summary>
    public int? AllottedTatMinutes { get; set; }
    public long? TatRuleId { get; set; }

    /// <summary>Frozen once EndedAt is set; null while open (callers compute the live value instead).</summary>
    public int? TatUsedMinutes { get; set; }
    public int? TatPausedMinutes { get; set; }
    public int? PauseCount { get; set; }

    /// <summary>Exact decimal seconds frozen on closure; null for open phases and legacy snapshots.</summary>
    public decimal? TatUsedSeconds { get; set; }
    public decimal? TatPausedSeconds { get; set; }

    public string CreatedBy { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
