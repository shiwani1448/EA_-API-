using System.Text.Json.Serialization;

namespace Jarvis5.Dtos.EaFms;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CreateEaTaskDto
{
    public long ModuleId { get; set; }
    public string BusinessRecordId { get; set; } = null!;
    public string Task { get; set; } = null!;
    public string? Description { get; set; }
    public string? Type { get; set; }
    public string? Subtype { get; set; }
    public long? WorkflowInstanceId { get; set; }
}

public class EaTaskResponseDto
{
    public long EaTaskId { get; set; }
    public long ModuleId { get; set; }
    /// <summary>Creation-time BusinessModule.Name snapshot — not a live lookup.</summary>
    public string ModuleName { get; set; } = null!;
    public string BusinessRecordId { get; set; } = null!;
    public string Task { get; set; } = null!;
    public string? Description { get; set; }
    public string? Type { get; set; }
    public string? Subtype { get; set; }
    public long? TatRuleId { get; set; }
    public int? AllottedTatMinutes { get; set; }
    /// <summary>Frozen once at completion; null before then or for no-TAT modules.</summary>
    public int? TatUsedMinutes { get; set; }
    /// <summary>
    /// Derived, never persisted: AllottedTatMinutes - TatUsedMinutes. Null if either
    /// input is null. Positive = completed within TAT; negative = exceeded TAT.
    /// </summary>
    public int? TatDifferenceMinutes { get; set; }
    /// <summary>Central task-execution snapshot: NotStarted | InProgress | Completed | Cancelled.
    /// Distinct from any module's own business status.</summary>
    public string ExecutionStatus { get; set; } = null!;
    /// <summary>First time actual execution began. Set once; never overwritten by resume/rework.</summary>
    public DateTime? StartedAt { get; set; }
    /// <summary>Terminal normal completion. Null for Cancelled tasks.</summary>
    public DateTime? CompletedAt { get; set; }
    /// <summary>Derived. Null when the module has no pause infrastructure (no WorkflowInstance).</summary>
    public bool? IsPaused { get; set; }
    /// <summary>Derived. Null when the module has no pause infrastructure.</summary>
    public int? PauseCount { get; set; }
    /// <summary>Derived. Null when the module has no pause infrastructure.</summary>
    public int? TotalPausedMinutes { get; set; }
    /// <summary>
    /// Derived live snapshot, never persisted: TAT consumed so far using the same
    /// canonical calculation as the frozen TatUsedMinutes. Null before execution starts,
    /// for no-TAT modules, and once Completed this equals the frozen TatUsedMinutes.
    /// </summary>
    public int? CurrentTatUsedMinutes { get; set; }
    /// <summary>Derived, never persisted: AllottedTatMinutes - CurrentTatUsedMinutes.</summary>
    public int? CurrentTatDifferenceMinutes { get; set; }
    public bool IsActive { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

/// <summary>
/// One normalized event in an EaTask's central execution timeline — Created, Started,
/// Paused, Resumed, Completed or Cancelled. This is NOT the module's full business
/// history (approval decisions, bookings, expenses, documents remain in their own
/// module-specific history endpoints); it is the small, cross-module execution-only
/// narrative built from existing authoritative sources (EaTask, WorkflowHistory,
/// WorkPause, AuditLog) — nothing here is fabricated.
/// </summary>
public class EaTaskHistoryEventDto
{
    public string EventType { get; set; } = null!;
    public string? PreviousExecutionStatus { get; set; }
    public string? NewExecutionStatus { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? PerformedBy { get; set; }
    public string? PerformedByName { get; set; }
    public bool? IsPaused { get; set; }
    public int? AllottedTatMinutes { get; set; }
    public int? CurrentTatUsedMinutes { get; set; }
    public int? CurrentTatDifferenceMinutes { get; set; }
    public DateTime? PauseStartedAt { get; set; }
    public DateTime? PauseEndedAt { get; set; }
    public int? PauseDurationMinutes { get; set; }
    /// <summary>Which authoritative table this event was reconstructed from — EaTask,
    /// WorkflowHistory, WorkPause, or AuditLog.</summary>
    public string Source { get; set; } = null!;
    public string? Notes { get; set; }
}
