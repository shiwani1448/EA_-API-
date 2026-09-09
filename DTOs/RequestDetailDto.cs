namespace Jarvis5.Dtos;

public class RequestDetailDto
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
    public string ExpectedBenefit { get; set; } = string.Empty;
    public long? ParentRequestId { get; set; }
    public DateTime? OverallStartDate { get; set; }
    public DateTime? OverallEndDate { get; set; }
    public List<PainPointDto> PainPoints { get; set; } = new();
    public List<AttachmentDto> Attachments { get; set; } = new();
    public RequestMetaDto Meta { get; set; } = new();
    public RequestHistoryDto? LatestHistory { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
