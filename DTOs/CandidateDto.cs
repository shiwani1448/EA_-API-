namespace hrms_api.DTOs;

public class CandidateDto
{
    public int RequisitionId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public int? YearsOfExperience { get; set; }
    public string? NoticePeriod { get; set; }
    public decimal? CurrentCtcLpa { get; set; }
    public decimal? ExpectedCtcLpa { get; set; }
    public string? KeySkills { get; set; }
    public string? Source { get; set; }
    public string? DateOfBirth { get; set; }
    public string? ResumeBase64 { get; set; }
    public string? ResumeFileName { get; set; }
    public string? ResumeContentType { get; set; }
}
