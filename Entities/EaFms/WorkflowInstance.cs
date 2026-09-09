namespace Jarvis5.Entities.EaFms;

/// <summary>
/// WorkflowInstance: represents the active workflow for an IntakeRequest.
/// Minimal fields to represent lifecycle and current status as per EA FMS roadmap.
/// </summary>
public class WorkflowInstance
{
    public long Id { get; set; }

    // The IntakeRequest this workflow instance may belong to (optional).
    // Workflows can be created for shared business records without an IntakeRequest.
    public long? IntakeRequestId { get; set; }
    public IntakeRequest? IntakeRequest { get; set; }

    // Linkage to an arbitrary business module record (optional)
    public long? BusinessModuleId { get; set; }
    public string? BusinessRecordId { get; set; }

    // Current status of the workflow instance (references EA FMS Status catalog).
    public int StatusId { get; set; }
    public Status? Status { get; set; }

    // Timestamps representing lifecycle progress.
    public DateTime StartedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Assignment / ownership for the workflow instance
    public string? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }

    // Archive timestamp
    public DateTime? ArchivedAt { get; set; }

    // TAT start timestamp (persisted, business rules to set later)
    public DateTime? TatStartedAt { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    // Audit
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }

    // Navigation to history entries
    public ICollection<WorkflowHistory> History { get; set; } = new List<WorkflowHistory>();
}
