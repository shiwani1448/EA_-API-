namespace Jarvis5.Dtos.DevelopmentPlan;

/// <summary>One selected stage inside a Task module's StageDetailsJson array —
/// shape matches the SCIH_StageMaster-driven planning form exactly.</summary>
public class StageDetailDto
{
    public long StageId { get; set; }
    public string StageName { get; set; } = string.Empty;
    public bool Selected { get; set; } = true;
    public long DoerId { get; set; }
    public string? Description { get; set; }
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RrrCount { get; set; }
    public string? Remarks { get; set; }
    public List<string> Checklist { get; set; } = new();
}
