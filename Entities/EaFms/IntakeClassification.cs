namespace Jarvis5.Entities.EaFms;

/// <summary>
/// IntakeClassification: classification labels attached to an IntakeRequest.
/// Minimal fields: classification name and optional details.
/// </summary>
public class IntakeClassification
{
    public int Id { get; set; }

    // FK to IntakeRequest
    public long IntakeRequestId { get; set; }
    public IntakeRequest? IntakeRequest { get; set; }

    // Classification label (e.g. "Contract", "Invoice", "Legal").
    public string Name { get; set; } = string.Empty;

    // Optional details about the classification decision.
    public string? Details { get; set; }

    public bool IsDeleted { get; set; }

    // Audit
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
