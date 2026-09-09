namespace Jarvis5.Entities.EaFms;

/// <summary>
/// Notification: EA FMS notification record metadata.
/// Stores recipient, category, content, reference to related entity and read state.
/// </summary>
public class Notification
{
    public long Id { get; set; }

    // Identifier of recipient (application user id, username or external id).
    public string RecipientId { get; set; } = string.Empty;
    public string? RecipientName { get; set; }

    // Notification category/type (e.g. "Followup", "Escalation").
    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    // Main notification content/body.
    public string? Message { get; set; }

    // Read state
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    // Optional reference to an entity (module name and record id)
    public string? ReferenceModule { get; set; }
    public string? ReferenceId { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    // Audit
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
