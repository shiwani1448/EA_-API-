namespace Jarvis5.Dtos.SnagList;

/// <summary>Body for PUT /api/snaglist/{id}/stage/{stageId} — patches only this one
/// stage inside the snag list's StageDetailsJson; siblings are untouched.</summary>
public class UpdateSnagStageDto
{
    public string DoerId { get; set; } = string.Empty;
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
