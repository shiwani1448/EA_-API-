namespace Jarvis5.Entities.EaFms;

/// <summary>
/// Escalation: records an escalation event tied to a Followup (and transitively an IntakeRequest).
/// Minimal model: which followup triggered it, level, timestamps, notes and audit.
/// </summary>
public class Escalation
{
    public long Id { get; set; }

    // The follow-up that triggered this escalation.
    public long? FollowupId { get; set; }
    public Followup? Followup { get; set; }

    // Optional linkage to workflow or intake or shared module record
    public long? WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }
    public long? IntakeRequestId { get; set; }
    public long? BusinessModuleId { get; set; }
    public string? BusinessRecordId { get; set; }

    // Escalation level (references EscalationLevel catalog)
    public int EscalationLevelId { get; set; }
    public EscalationLevel? EscalationLevel { get; set; }

    // When the escalation was initiated.
    public DateTime InitiatedAt { get; set; }

    // When the escalation was resolved, if any.
    public DateTime? ResolvedAt { get; set; }

    // Optional note about the escalation action.
    public string? Notes { get; set; }

    // Escalation targets and acknowledgement/resolution metadata
    public string? EscalatedToId { get; set; }
    public string? EscalatedToName { get; set; }

    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedById { get; set; }
    public string? AcknowledgedByName { get; set; }
    public string? AcknowledgementNote { get; set; }

    public string? ResolvedById { get; set; }
    public string? ResolvedByName { get; set; }
    public string? ResolutionNote { get; set; }

    // Optional schedule for next escalation
    public DateTime? NextEscalationAt { get; set; }
    public int? NextEscalationLevelId { get; set; }

    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public bool IsDeleted { get; set; }

    // Audit
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
