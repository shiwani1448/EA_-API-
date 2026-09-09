namespace hrms_api.Models;

public class Candidate
{
    public int CandidateId { get; set; }
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
    public string? ResumePath { get; set; }
    public string? ResumeFileName { get; set; }
    public string? ResumeContentType { get; set; }
    public int? AssignedHRId { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string CurrentStage { get; set; } = "applied";
    public string CurrentStatus { get; set; } = "pending";
    public DateTime? ShortlistingDate { get; set; }
    public bool IsDeleted { get; set; }
    public List<CandidateActivity> CandidateActivities { get; set; } = new();
}
