namespace hrms_api.DTOs;

public class CandidateResumeUploadDto
{
    public string? ResumeBase64 { get; set; }
    public string? ResumeFileName { get; set; }
    public string? ResumeContentType { get; set; }
}
