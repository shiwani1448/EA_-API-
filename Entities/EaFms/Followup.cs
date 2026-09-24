namespace Jarvis5.Entities.EaFms;

/// <summary>
/// Followup: scheduled follow-up activity for an IntakeRequest or WorkflowInstance.
/// Minimal data: due date, optional completion timestamp, note, and audit.
/// </summary>
public class Followup
{
    public long Id { get; set; }

    // The Followup's own central execution task (Follow-up module, TaskType Actual only —
    // no Review/Rework). Created once at Followup creation time (see FollowupService.CreateAsync,
    // mirroring DelegationService.CreateCoreAsync); nullable only so pre-existing rows can be
    // backfilled without a schema-level NOT NULL blocking the migration. Every Followup created
    // going forward always has one. This is a completely separate concept from BusinessModuleId/
    // BusinessRecordId below, which identify what OTHER record (Meeting, Delegation, ...) this
    // follow-up is chasing — that source task's own EaTaskId/Task/Stage/IsPaused are still
    // surfaced unchanged elsewhere on FollowupResponseDto.
    public long? EaTaskId { get; set; }
    public EaTask? EaTask { get; set; }

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
    public string? DoerId { get; set; }
    public string? DoerName { get; set; }

    // Optional priority specific to followup
    public int? PriorityLevelId { get; set; }

    // Reminder and scheduling
    public DateTime? ReminderAt { get; set; }
    // Reminder delivery configuration (one per Followup). Configuration only: nothing is sent yet.
    public bool ReminderSendEmail { get; set; }
    public bool ReminderSendWhatsApp { get; set; }
    // Users.Id of the reminder recipient; the Email is resolved from Users.Email, never stored here.
    public int? ReminderRecipientUserId { get; set; }
    // Raw mobile snapshot selected by the frontend from the external Employee API; backend does not normalize it.
    public string? ReminderWhatsAppNumber { get; set; }
    // Snapshot from the externally-owned Employee API selection, independent from legacy Users.Id.
    public string? ReminderRecipientEmployeeId { get; set; }
    public string? ReminderRecipientName { get; set; }
    public string? ReminderRecipientEmail { get; set; }
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
    // Frontend-supplied actor snapshots (not verified by EA); CreatedBy/ModifiedBy keep the display value.
    public string? CreatedByEmployeeId { get; set; }
    public string? CreatedByEmployeeName { get; set; }
    public string? ModifiedByEmployeeId { get; set; }
    public string? ModifiedByEmployeeName { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
