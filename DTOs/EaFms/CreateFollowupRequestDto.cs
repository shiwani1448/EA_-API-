using System;
using System.Text.Json.Serialization;

namespace Jarvis5.Dtos.EaFms;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CreateFollowupRequestDto
{
    public long? IntakeRequestId { get; set; }
    public long? WorkflowInstanceId { get; set; }
    public DateTime DueAt { get; set; }
    /// <summary>EA's remark (canonical). Stored in Followup.Note.</summary>
    public string? Remark { get; set; }
    /// <summary>Backward-compatible alias of Remark. If both are sent they must match.</summary>
    public string? Note { get; set; }
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
    public DateTime? NextFollowupAt { get; set; }
    /// <summary>Frontend-supplied actor snapshot (operator's employee id). Stored as attribution; not verified.</summary>
    public string? EmployeeId { get; set; }
    /// <summary>Frontend-supplied actor snapshot (operator's employee name). Stored as attribution; not verified.</summary>
    public string? EmployeeName { get; set; }
    public string? ResponseOwnerId { get; set; }
    public string? ResponseOwnerName { get; set; }
    public DateTime? ExpectedResponseAt { get; set; }
    public string? WaitingOnId { get; set; }
    public string? WaitingOnName { get; set; }
    public string? WaitingOnExternal { get; set; }
    public int? SequenceNumber { get; set; }

    // Optional source linkage; both fields may be null for standalone follow-ups.
    public long? BusinessModuleId { get; set; }
    public string? BusinessRecordId { get; set; }
}
