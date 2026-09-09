namespace Jarvis5.Dtos.EaFms;

public class CreateNotificationRequestDto
{
    // Required: recipient identifier (canonical string)
    public string RecipientId { get; set; } = string.Empty;

    // Optional/name for display
    public string? RecipientName { get; set; }

    // Type/category (e.g. "Followup", "Escalation")
    public string Type { get; set; } = string.Empty;

    // Title (short)
    public string Title { get; set; } = string.Empty;

    // Optional body
    public string? Message { get; set; }

    // Optional reference
    public string? ReferenceModule { get; set; }
    public string? ReferenceId { get; set; }
}
