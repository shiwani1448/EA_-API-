namespace Jarvis5.Entities;

public class SCIHRequest
{
    public long Id { get; set; }
    public string RequestNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public string RaisedBy { get; set; } = string.Empty;
    public DateTime RaisedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int CurrentStage { get; set; }
    public string Priority { get; set; } = string.Empty;
    public decimal OverallProgress { get; set; }
    public string ExpectedBenefit { get; set; } = string.Empty;
    public long? ParentRequestId { get; set; }
    public DateTime? OverallStartDate { get; set; }
    public DateTime? OverallEndDate { get; set; }

    /// <summary>Raw JSON array — see PainPointDto for shape.</summary>
    public string PainPointsJson { get; set; } = "[]";

    /// <summary>Raw JSON array — see AttachmentDto for shape.</summary>
    public string AttachmentsJson { get; set; } = "[]";

    /// <summary>Raw JSON object — see RequestMetaDto for shape.</summary>
    public string MetaJson { get; set; } = "{}";

    public bool IsDeleted { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public long? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
