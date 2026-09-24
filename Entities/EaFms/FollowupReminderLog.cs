namespace Jarvis5.Entities.EaFms;

/// <summary>
/// A record that a reminder was handed off to the EA for this Followup — logged explicitly by
/// the frontend when the user clicks "Open WhatsApp" / opens the prefilled email
/// (FollowupService.SendEmailAsync/SendWhatsAppAsync themselves send nothing; this table is the
/// only record that a handoff actually happened). SentAt therefore means "handed off to the EA
/// at this instant", not "delivered to the recipient" — there is no delivery confirmation here.
/// </summary>
public class FollowupReminderLog
{
    public long Id { get; set; }
    public long FollowupId { get; set; }
    public Followup Followup { get; set; } = null!;

    /// <summary>Email | WhatsApp.</summary>
    public string Channel { get; set; } = string.Empty;
    /// <summary>The recipient's email address or phone number, matching Channel.</summary>
    public string Recipient { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string Message { get; set; } = string.Empty;

    public DateTime SentAt { get; set; }
    public string? SentById { get; set; }
    public string? SentByName { get; set; }

    public DateTime CreatedDate { get; set; }
}
