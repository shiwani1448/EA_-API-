namespace Jarvis5.Dtos.SnagList;

/// <summary>One selected stage inside a Snag List's StageDetailsJson array — shape
/// matches the SCIH_StageMaster-driven stage-selection form exactly (same fields as
/// Development Planning's StageDetailDto, except DoerId which is a free-text string
/// here rather than a numeric user id).</summary>
public class SnagStageDetailDto
{
    public long StageId { get; set; }
    public string StageName { get; set; } = string.Empty;
    public bool Selected { get; set; } = true;
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
