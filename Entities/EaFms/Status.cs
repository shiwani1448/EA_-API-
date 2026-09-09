namespace Jarvis5.Entities.EaFms;

/// <summary>
/// Status: generic status catalog for EA FMS (e.g. Pending, In Progress, Completed).
/// Minimal fields to allow ordering and activation.
/// </summary>
public class Status
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    // Audit
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
