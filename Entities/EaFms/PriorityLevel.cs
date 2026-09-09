namespace Jarvis5.Entities.EaFms;

/// <summary>
/// PriorityLevel: catalog of priority levels (e.g. Low, Medium, High).
/// </summary>
public class PriorityLevel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    // Numeric level where higher value indicates higher priority.
    public int Level { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    // Audit
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
