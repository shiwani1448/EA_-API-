namespace Jarvis5.Entities.EaFms;

/// <summary>
/// WorkAssignment: records assignment and reassignment history for EA work contexts.
/// </summary>
public class WorkAssignment
{
    public long Id { get; set; }

    public long? IntakeRequestId { get; set; }
    public IntakeRequest? IntakeRequest { get; set; }

    public long? WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }

    public long? FollowupId { get; set; }
    public Followup? Followup { get; set; }

    public string AssignedToId { get; set; } = string.Empty;
    public string? AssignedToName { get; set; }

    public string? AssignedById { get; set; }
    public string? AssignedByName { get; set; }

    public DateTime AssignedAt { get; set; }

    public DateTime? UnassignedAt { get; set; }
    public string? UnassignedById { get; set; }
    public string? UnassignedByName { get; set; }

    public string? AssignmentType { get; set; }
    public string? Reason { get; set; }

    public bool IsCurrent { get; set; }
    public bool IsDeleted { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
