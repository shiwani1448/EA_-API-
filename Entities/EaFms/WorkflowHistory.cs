namespace Jarvis5.Entities.EaFms;

/// <summary>
/// WorkflowHistory: records state transitions for a WorkflowInstance.
/// Minimal fields: from/to status, who changed, when, and optional notes.
/// </summary>
public class WorkflowHistory
{
    public long Id { get; set; }

    // FK to WorkflowInstance
    public long WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }

    // Status transition
    public int? FromStatusId { get; set; }
    public int? ToStatusId { get; set; }

    // Optional note explaining the transition
    public string? Notes { get; set; }

    public DateTime ChangedAt { get; set; }

    // Audit
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }

    // Optional stage owner for the period following this transition
    public string? StageOwnerId { get; set; }
    public string? StageOwnerName { get; set; }

    // Transition type (e.g. MANUAL, AUTO, SYSTEM) - controlled set
    public string? TransitionType { get; set; }
}
