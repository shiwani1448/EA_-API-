using System;

namespace Jarvis5.Dtos.EaFms;

public class FollowupResponseDto
{
    public long Id { get; set; }
    public long? IntakeRequestId { get; set; }
    public long? WorkflowInstanceId { get; set; }
    public long? BusinessModuleId { get; set; }
    public string? BusinessModuleCode { get; set; }
    public string? BusinessModuleName { get; set; }
    public string? BusinessRecordId { get; set; }
    public string? BusinessRecordTitle { get; set; }
    public string? PriorityLevelName { get; set; }
    public DateTime DueAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>Backward-compatible alias of <see cref="Remark"/> (same stored value, Followup.Note).</summary>
    public string? Note { get; set; }
    /// <summary>EA's follow-up/reminder remark. Canonical name for Followup.Note.</summary>
    public string? Remark { get; set; }

    // Central task context, derived from the EaTask matching BusinessModuleId + BusinessRecordId.
    // Null for follow-ups with no business source (e.g. intake/standalone) or no matching task.
    /// <summary>ea_tasks.Id — a different identity from BusinessRecordId.</summary>
    public long? EaTaskId { get; set; }
    /// <summary>BusinessModule.Name (same value as BusinessModuleName).</summary>
    public string? ModuleName { get; set; }
    /// <summary>EaTask.Task.</summary>
    public string? Task { get; set; }
    /// <summary>Central EaTask.ExecutionStatus: NotStarted | InProgress | Completed | Cancelled.</summary>
    public string? Stage { get; set; }
    /// <summary>Derived from the task's open WorkPause; Stage stays InProgress while paused. Null when the task has no pause infrastructure.</summary>
    public bool? IsPaused { get; set; }
    public string? Subject { get; set; }
    public string? Type { get; set; }
    public string? DoerId { get; set; }
    public string? DoerName { get; set; }
    public int? PriorityLevelId { get; set; }
    public DateTime? ReminderAt { get; set; }
    public bool ReminderSendEmail { get; set; }
    public bool ReminderSendWhatsApp { get; set; }
    /// <summary>Legacy internal Users.Id retained only for backward compatibility; current frontend-supplied recipient snapshot flows do not require or resolve it.</summary>
    public int? ReminderRecipientUserId { get; set; }
    public string? ReminderRecipientEmployeeId { get; set; }
    public string? ReminderRecipientName { get; set; }
    public string? ReminderWhatsAppNumber { get; set; }
    public string? ReminderRecipientEmail { get; set; }
    public ReminderRecipientResponseDto? Recipient { get; set; }
    public FollowupWhatsAppHandoffResponseDto? WhatsApp { get; set; }
    public FollowupEscalationResponseDto? Escalation { get; set; }
    public DateTime? LastFollowupAt { get; set; }
    public string? WaitingOnId { get; set; }
    public string? WaitingOnName { get; set; }
    public string? WaitingOnExternal { get; set; }
    public string? ResponseOwnerId { get; set; }
    public string? ResponseOwnerName { get; set; }
    public DateTime? ExpectedResponseAt { get; set; }
    public int? SequenceNumber { get; set; }
    public string? CompletionNote { get; set; }
    public string? OutcomeCode { get; set; }
    public DateTime? NextFollowupAt { get; set; }
    public string? CompletedById { get; set; }
    public string? CompletedByName { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsOverdue { get; set; }
    public bool IsDeleted { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    // Frontend-supplied actor snapshots (attribution only; not verified by EA).
    public string? CreatedByEmployeeId { get; set; }
    public string? CreatedByEmployeeName { get; set; }
    public string? ModifiedByEmployeeId { get; set; }
    public string? ModifiedByEmployeeName { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

public sealed class ReminderRecipientResponseDto
{
    public string? EmployeeId { get; set; }
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
public sealed class FollowupWhatsAppHandoffResponseDto
{
    public string Message { get; set; } = string.Empty;
}

public sealed class FollowupEscalationResponseDto
{
    public long Id { get; set; }
    public int EscalationLevelId { get; set; }
    public string? EscalationLevelName { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool IsAcknowledged { get; set; }
    public bool IsResolved { get; set; }
}

/// <summary>On-demand KPI counts for the currently filtered Followup scope.</summary>
public sealed class FollowupSummaryResponseDto
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int DueToday { get; set; }
    public int Overdue { get; set; }
    public int UpcomingReminders { get; set; }
    public int Completed { get; set; }
    public int Escalated { get; set; }
}
/// <summary>Explicit frontend WhatsApp handoff; this is not a delivery result.</summary>
public sealed class FollowupWhatsAppActionResponseDto
{
    public string Phone { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}