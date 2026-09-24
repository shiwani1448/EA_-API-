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
    public long? SourceEaTaskId { get; set; }
    public bool? SourceIsPaused { get; set; }
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

    // ---- The Followup's OWN execution lifecycle (Start/Pause/Resume/Complete), Actual-phase
    // TAT only — a completely separate concept from EaTaskId/Task/Stage/IsPaused above, which
    // describe the OTHER record (Meeting/Delegation/...) this follow-up is chasing. Named with
    // a "Followup" prefix specifically to avoid colliding with those existing fields. Read from
    // the Followup's own central EaTask/WorkPauses/FollowupPhaseTat, nothing stored twice. ----

    /// <summary>The Followup's own central execution task id — distinct from EaTaskId above
    /// (the matched SOURCE record's task). Null only for a pre-backfill legacy row.</summary>
    public long? FollowupEaTaskId { get; set; }
    /// <summary>NotStarted | InProgress | Completed — the Followup's own execution status.</summary>
    public string? ExecutionStatus { get; set; }
    /// <summary>True only while InProgress and the Followup's own pause anchor has an open WorkPause.</summary>
    public bool IsFollowupPaused { get; set; }
    public DateTime? StartedAt { get; set; }
    public string? StartedById { get; set; }
    public string? StartedByName { get; set; }
    /// <summary>Always "Actual" once started (Follow-up has no Review/Rework), else null.</summary>
    public string? CurrentPhase { get; set; }
    public DateTime? CurrentPhaseStartedAt { get; set; }
    /// <summary>Configured TAT snapshot taken at creation/Start (Type + "Actual"). Null when no rule is configured.</summary>
    public int? AllottedTatMinutes { get; set; }
    /// <summary>Live elapsed-minus-paused minutes (same TatSummaryCalculator formula Meeting/Delegation use). Null before Start and when there is no TAT.</summary>
    public int? TatUsedMinutes { get; set; }
    public int? TatPausedMinutes { get; set; }
    public MeetingTatSummaryDto TatSummary { get; set; } = new();
    /// <summary>Exactly one entry once started (Actual, ReviewCycleNumber 0) — a list only for
    /// contract symmetry with Delegation's own PhaseTat; Followup never has more than one.</summary>
    public List<FollowupPhaseTatDto> PhaseTat { get; set; } = new();
}

/// <summary>Same shape as DelegationPhaseTatDto, plus explicit StartedBy/EndedBy actor fields.
/// TaskType is always "Actual" and ReviewCycleNumber always 0 for a Followup.</summary>
public class FollowupPhaseTatDto
{
    public string TaskType { get; set; } = string.Empty;
    public int ReviewCycleNumber { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? StartedById { get; set; }
    public string? StartedByName { get; set; }
    public string? EndedById { get; set; }
    public string? EndedByName { get; set; }
    public int? AllottedTatMinutes { get; set; }
    public int? TatUsedMinutes { get; set; }
    public int? TatPausedMinutes { get; set; }
    public int? PauseCount { get; set; }
    public int? TatDifferenceMinutes { get; set; }
    public decimal? TatUsedSeconds { get; set; }
    public decimal? TatPausedSeconds { get; set; }
    public decimal? TatDifferenceSeconds { get; set; }
}

/// <summary>Optional body for POST /api/ea/followups/{id}/pause. Same reason concept as Delegation's own pause request.</summary>
public class FollowupPauseRequestDto
{
    /// <summary>Optional pause reason (max 2000). Defaults to "Follow-up paused" when omitted or blank.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(2000)]
    public string? PauseReason { get; set; }
}

// ============================================================
// Reminder log (§5) — an explicit record that a reminder handoff happened. The backend sends
// nothing itself; the frontend calls this when the user actually opens the email/WhatsApp handoff.
// ============================================================

public class LogFollowupReminderRequestDto
{
    /// <summary>Email | WhatsApp.</summary>
    public string Channel { get; set; } = string.Empty;
    /// <summary>The recipient's email address or phone number, matching Channel.</summary>
    public string Recipient { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string Message { get; set; } = string.Empty;
    /// <summary>Accepted for backward compatibility but IGNORED: SentById/SentByName always come
    /// from the caller's token (§0), never from the request body.</summary>
    public string? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
}

public class FollowupReminderLogResponseDto
{
    public long Id { get; set; }
    public long FollowupId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public string? SentById { get; set; }
    public string? SentByName { get; set; }
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
    /// <summary>Count of Followups whose own execution status is NotStarted (never Started).</summary>
    public int NotStarted { get; set; }
}
/// <summary>
/// Explicit frontend Email handoff (same pattern as the WhatsApp handoff); this is not a delivery result — the backend sends nothing.
/// The frontend opens <see cref="MailtoUrl"/> (or builds its own link from Email/Subject/Body) and the user sends the mail manually.
/// </summary>
public sealed class FollowupEmailActionResponseDto
{
    /// <summary>The persisted frontend-supplied ReminderRecipientEmail snapshot.</summary>
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    /// <summary>Plain-text body (unencoded).</summary>
    public string Body { get; set; } = string.Empty;
    /// <summary>RFC 6068 mailto: URI with the recipient, subject and body prefilled and URI-encoded.</summary>
    public string MailtoUrl { get; set; } = string.Empty;
}

/// <summary>Explicit frontend WhatsApp handoff; this is not a delivery result.</summary>
public sealed class FollowupWhatsAppActionResponseDto
{
    public string Phone { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}