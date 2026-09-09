namespace hrms_api.DTOs;

public class CandidateBGVDocumentDto
{
    public int CandidateId { get; set; }
    public string? CheckType { get; set; }
    public string? DocumentType { get; set; }
    public string? FileBase64 { get; set; }
    public string? FileName { get; set; }
    public string? Status { get; set; }
    public string? Remarks { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime? UploadedAt { get; set; }
}

public class CandidateBGVDocumentResponseDto
{
    public int Id { get; set; }
    public int CandidateId { get; set; }
    public string? CheckType { get; set; }
    public string? DocumentType { get; set; }
    public string? FilePath { get; set; }
    public string? Status { get; set; }
    public string? Remarks { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }
}
