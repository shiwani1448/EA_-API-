namespace Jarvis5.Dtos.DevelopmentPlan;

/// <summary>Body for PUT /api/development-plan/{taskId}/stage/{stageId} — patches
/// only this one stage inside the module's StageDetailsJson; siblings are untouched.</summary>
public class UpdateStageDto
{
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
