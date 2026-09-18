namespace Jarvis5.Entities.EaFms;

public class EaTask
{
    public long Id { get; set; }
    public long BusinessModuleId { get; set; }
    public BusinessModule BusinessModule { get; set; } = null!;
    // Creation-time snapshot of BusinessModule.Name, backfilled for all existing rows.
    // Display/history value only — BusinessModuleId remains the canonical FK; renaming
    // the module later never retroactively changes what past tasks show.
    public string ModuleName { get; set; } = null!;
    public string BusinessRecordId { get; set; } = null!;
    public string Task { get; set; } = null!;
    public string? Description { get; set; }
    // Classification snapshot actually used to resolve TAT at creation (Meeting), or
    // preserved for classification/audit only when no TAT applies (Approval). Null when
    // the creating module never established a classification (Travel, Delegation).
    public string? Type { get; set; }
    public string? Subtype { get; set; }
    // The exact TatRule row whose TatMinutes produced AllottedTatMinutes, when TAT was
    // actually resolved. Never fabricated for no-TAT modules or unprovable history.
    public long? TatRuleId { get; set; }
    public TatRule? TatRule { get; set; }
    // Snapshot at creation; module configuration changes never update this value.
    public int? AllottedTatMinutes { get; set; }
    // Frozen once, at completion, from the same canonical elapsed-minus-paused
    // calculation Meeting already uses for display (WorkPauseClassifier.GetPausedDuration).
    // Null before completion and for modules with no TAT/completion concept.
    public int? TatUsedMinutes { get; set; }
    // Central task-execution snapshot: NotStarted | InProgress | Completed | Cancelled
    // (Jarvis5.Common.EaFms.EaTaskExecutionStatus). Distinct from any module's own
    // business status (Meeting.statusName, Travel.BusinessState, Approval.WorkflowStatus,
    // Delegation.Status), which remain separate and unchanged.
    public string ExecutionStatus { get; set; } = null!;
    // First time actual execution began. Set once; never overwritten by resume/rework.
    public DateTime? StartedAt { get; set; }
    // When execution reached terminal normal completion. Null for Cancelled tasks —
    // the cancellation moment remains available through audit/history instead.
    public DateTime? CompletedAt { get; set; }
    public long? WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
}
