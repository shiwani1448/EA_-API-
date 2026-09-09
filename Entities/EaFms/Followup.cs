namespace Jarvis5.Entities.EaFms;

/// <summary>
/// Followup: scheduled follow-up activity for an IntakeRequest or WorkflowInstance.
/// Minimal data: due date, optional completion timestamp, note, and audit.
/// </summary>
public class Followup
{
    public long Id { get; set; }

    // Optional link to an IntakeRequest. Followups may instead reference a shared business record.
    public long? IntakeRequestId { get; set; }
    public IntakeRequest? IntakeRequest { get; set; }

    // Optional linkage to an arbitrary business module record
    public long? BusinessModuleId { get; set; }
    public string? BusinessRecordId { get; set; }

    // Optional link to an active workflow instance.
    public long? WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }

    // When the follow-up is due (UTC).
    public DateTime DueAt { get; set; }

    // When the follow-up was completed (UTC).
    public DateTime? CompletedAt { get; set; }

    // Short note or instruction for the follow-up.
    public string? Note { get; set; }

    // Optional subject/title for the followup
    public string? Subject { get; set; }

    // Followup type (e.g. Reminder, Action)
    public string? Type { get; set; }

    // Assignment
    public string? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }

    // Optional priority specific to followup
    public int? PriorityLevelId { get; set; }

    // Reminder and scheduling
    public DateTime? ReminderAt { get; set; }
    public DateTime? NextFollowupAt { get; set; }

    // Waiting/on and completion outcome
    public string? WaitingOnId { get; set; }
    public string? WaitingOnName { get; set; }
    public string? WaitingOnExternal { get; set; }

    // Response owner and expected response timestamp
    public string? ResponseOwnerId { get; set; }
    public string? ResponseOwnerName { get; set; }
    public DateTime? ExpectedResponseAt { get; set; }
    // Timestamp of last actual follow-up/reminder action performed (server-controlled)
    public DateTime? LastFollowupAt { get; set; }
    public string? CompletionNote { get; set; }
    public string? OutcomeCode { get; set; }
    public int? SequenceNumber { get; set; }
    public string? CompletedById { get; set; }
    public string? CompletedByName { get; set; }

    public bool IsDeleted { get; set; }

    // Audit
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
