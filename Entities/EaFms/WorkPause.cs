namespace Jarvis5.Entities.EaFms;

/// <summary>
/// WorkPause: records pause/waiting periods for Intake/Workflow/Followup contexts.
/// </summary>
public class WorkPause
{
    public long Id { get; set; }

    public long? IntakeRequestId { get; set; }
    public IntakeRequest? IntakeRequest { get; set; }

    public long? WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }

    public long? FollowupId { get; set; }
    public Followup? Followup { get; set; }

    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }

    public string? Reason { get; set; }

    public string? WaitingOnId { get; set; }
    public string? WaitingOnName { get; set; }
    public string? WaitingOnExternal { get; set; }

    public string? ResponseOwnerId { get; set; }
    public string? ResponseOwnerName { get; set; }

    public DateTime? ExpectedResponseAt { get; set; }

    /// <summary>
    /// When the dependency/request was sent to the waiting-on party (distinct from StartAt pause timing).
    /// </summary>
    public DateTime? RequestSentAt { get; set; }

    public string? ResumedById { get; set; }
    public string? ResumedByName { get; set; }
    public string? ResumedReason { get; set; }

    public bool IsDeleted { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
