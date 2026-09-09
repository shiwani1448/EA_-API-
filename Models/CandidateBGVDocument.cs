namespace hrms_api.Models;

public class CandidateBGVDocument
{
    public int Id { get; set; }
    public int CandidateId { get; set; }
    public string? CheckType { get; set; }
    public string? DocumentType { get; set; }
    public string? FilePath { get; set; }
    public string? Status { get; set; }
    public string? Remarks { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Candidate? Candidate { get; set; }
}
