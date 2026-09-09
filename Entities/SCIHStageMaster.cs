namespace Jarvis5.Entities;

/// <summary>Catalog of stages available for a Task module (Mind Mapping, Backend,
/// Frontend, AI, Testing, Deployment, …), loaded dynamically by the frontend
/// when composing a module's stage selection.</summary>
public class SCIHStageMaster
{
    public long Id { get; set; }
    public string StageName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    /// <summary>Raw JSON array of strings — the default checklist for this stage.</summary>
    public string ChecklistJson { get; set; } = "[]";

    public bool IsActive { get; set; } = true;

    public bool IsTestingStage { get; set; }

    /// <summary>Base/reference TAT values (hours) per complexity level. The backend
    /// only stores and returns these — all TAT calculation happens in the frontend.</summary>
    public decimal TatSmallHours { get; set; }
    public decimal TatMediumHours { get; set; }
    public decimal TatLargeHours { get; set; }

    public bool IsDeleted { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
