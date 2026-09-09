namespace hrms_api.DTOs;

public class HiringRequestDto
{
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public int? NumberOfPosition { get; set; }
    public string? Priority { get; set; }
    public DateTime? RequiredByDate { get; set; }
    public string? ExperienceRequired { get; set; }
    public string? ReasonForHiring { get; set; }
    public string? RequestById { get; set; }
    public bool? IsApprovedByDirector { get; set; }
    public string? DirectorName { get; set; }
}
