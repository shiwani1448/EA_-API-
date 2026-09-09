namespace Jarvis5.Entities.EaFms;

/// <summary>
/// EscalationLevel: catalog of escalation severity levels (L1..L5).
/// Represents the roadmap-defined levels (EA reminder, Department Head, Director, etc.).
/// </summary>
public class EscalationLevel
{
    public int Id { get; set; }

    // Code like "L1", "L2".
    public string Code { get; set; } = string.Empty;

    // Human-friendly name (e.g. "EA reminder").
    public string Name { get; set; } = string.Empty;

    // Optional description explaining the level.
    public string? Description { get; set; }

    // Numeric order (1..5) to represent severity.
    public int Level { get; set; }

    public bool IsDeleted { get; set; }

    // Audit
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
