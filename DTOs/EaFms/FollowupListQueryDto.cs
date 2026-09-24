namespace Jarvis5.Dtos.EaFms;

public class FollowupListQueryDto
{
    public long? BusinessModuleId { get; set; }
    public string? BusinessRecordId { get; set; }
    public string? DoerId { get; set; }
    public string? WaitingOnId { get; set; }
    public string? ResponseOwnerId { get; set; }
    public int? PriorityLevelId { get; set; }
    public string? Status { get; set; }
    public bool? IsCompleted { get; set; }
    public bool? IsOverdue { get; set; }
    public DateTime? DueFrom { get; set; }
    public DateTime? DueTo { get; set; }
    public string? Search { get; set; }
    public string? Stage { get; set; }
    public bool? IsPaused { get; set; }
    public string? ReminderRecipientEmployeeId { get; set; }
    public bool? ReminderSendWhatsApp { get; set; }
    public bool? ReminderSendEmail { get; set; }
    public DateTime? ReminderFrom { get; set; }
    public DateTime? ReminderTo { get; set; }
    public bool? IsEscalated { get; set; }
    public int? EscalationLevelId { get; set; }
    /// <summary>Filters by the Followup's OWN execution status: notstarted | started (InProgress).
    /// Distinct from the existing Stage/IsPaused filters above, which reference the matched
    /// source-record task, not the Followup's own lifecycle.</summary>
    public string? View { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
