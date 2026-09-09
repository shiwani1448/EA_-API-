namespace hrms_api.Models;

public class HiringRequest
{
    public int RequestId { get; set; }
    public int? JDID { get; set; }
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public int? NumberOfPosition { get; set; }
    public string? Priority { get; set; }
    public DateTime? RequiredByDate { get; set; }
    public string? ExperienceRequired { get; set; }
    public string? ReasonForHiring { get; set; }
    public string? RequestById { get; set; }
    public string HrStatus { get; set; } = "Pending";
    public DateTime? HrAcceptDate { get; set; }
    public bool? IsApprovedByDirector { get; set; }
    public string? DirectorName { get; set; }
    public string DirectorStatus { get; set; } = "Pending";
    public DateTime? DirectorActionDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int? ExperienceMinYears { get; set; }
    public int? ExperienceMaxYears { get; set; }
    public decimal? BudgetMinLpa { get; set; }
    public decimal? BudgetMaxLpa { get; set; }
    public string? AcceptableNoticePeriods { get; set; }
    public bool IsDeleted { get; set; }
}

