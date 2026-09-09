namespace Jarvis5.Dtos;

public class RequestListItemDto
{
    public long Id { get; set; }
    public string RequestNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public string RaisedBy { get; set; } = string.Empty;
    public DateTime RaisedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int CurrentStage { get; set; }
    public string CurrentStageName { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public decimal OverallProgress { get; set; }
}
