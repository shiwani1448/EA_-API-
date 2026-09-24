namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One AI-drafted reminder message for a Followup — same "one table per real AI task, real
/// typed columns" convention every other AI task table in this codebase uses. Nothing is
/// ever sent from this row alone: IsApplied/AppliedAt/AppliedRecipientEmail record only that
/// the EA reviewed the draft and asked FollowupAiService to actually send it via
/// IEaReminderEmailSender — the real, pre-existing SMTP sender this feature wires up for the
/// first time (FollowupService.SendEmailAsync only ever built a mailto: handoff link).
/// </summary>
public class FollowupReminderSuggestion
{
    public long Id { get; set; }
    public long FollowupId { get; set; }

    public string? SuggestedSubject { get; set; }
    public string? SuggestedBody { get; set; }
    public string? Reasoning { get; set; }
    public string? WarningMessage { get; set; }

    public bool IsApplied { get; set; }
    public DateTime? AppliedAt { get; set; }
    public string? AppliedRecipientEmail { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
