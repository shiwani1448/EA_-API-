namespace Jarvis5.Dtos.DevelopmentPlan;

/// <summary>Response shape for GET /api/stage-master — lets the frontend load
/// the available stages and their default checklists dynamically.</summary>
public class StageMasterDto
{
    public long Id { get; set; }
    public string StageName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsTestingStage { get; set; }
    public TatHoursDto TatHours { get; set; } = new();
    public List<string> Checklist { get; set; } = new();
}

/// <summary>Base/reference TAT values (hours) per complexity level. The frontend
/// selects the value matching the chosen complexity and computes the estimated
/// TAT itself — the backend performs no calculation on these values.</summary>
public class TatHoursDto
{
    public decimal Small { get; set; }
    public decimal Medium { get; set; }
    public decimal Large { get; set; }
}
