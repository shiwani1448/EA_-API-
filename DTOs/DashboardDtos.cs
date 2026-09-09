namespace hrms_api.DTOs;

public class DashboardDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int? HrId { get; set; }
    public string? HrName { get; set; }
    public DashboardOverviewDto Overview { get; set; } = new();
    public DashboardPipelineDto Pipeline { get; set; } = new();
    public DashboardRecruiterActionDto RecruiterActions { get; set; } = new();
    public List<DashboardSourceDto> SourcePerformance { get; set; } = new();
    public List<DashboardRequisitionDto> AgingRequisitions { get; set; } = new();
    public List<DashboardCandidateDto> TopCandidates { get; set; } = new();
    public List<DashboardActivityDto> RecentActivity { get; set; } = new();
}

public class DashboardOverviewDto
{
    public int TotalHiringRequests { get; set; }
    public int OpenHiringRequests { get; set; }
    public int PendingHrAcceptance { get; set; }
    public int PendingDirectorApproval { get; set; }
    public int TotalOpenPositions { get; set; }
    public int TotalCandidates { get; set; }
    public int NewCandidates { get; set; }
    public int ShortlistedCandidates { get; set; }
    public int OfferedCandidates { get; set; }
    public int JoinedCandidates { get; set; }
    public int OnboardingCandidates { get; set; }
    public int OnboardingCompletedCandidates { get; set; }
    public int RejectedCandidates { get; set; }
    public decimal OfferToJoinPercentage { get; set; }
    public decimal CandidateToJoinPercentage { get; set; }
}

public class DashboardPipelineDto
{
    public List<DashboardCountDto> ByStage { get; set; } = new();
    public List<DashboardCountDto> ByStatus { get; set; } = new();
    public List<DashboardCountDto> OnboardingProgress { get; set; } = new();
    public List<DashboardCountDto> ByPriority { get; set; } = new();
    public List<DashboardCountDto> ByDepartment { get; set; } = new();
}

public class DashboardRecruiterActionDto
{
    public int OverdueFollowUps { get; set; }
    public int DueTodayFollowUps { get; set; }
    public int UpcomingFollowUps { get; set; }
    public int InterviewsToday { get; set; }
    public int UpcomingInterviews { get; set; }
    public int StaleCandidates { get; set; }
    public List<DashboardCandidateDto> OverdueFollowUpCandidates { get; set; } = new();
    public List<DashboardCandidateDto> TodayFollowUpCandidates { get; set; } = new();
    public List<DashboardActivityDto> UpcomingInterviewDetails { get; set; } = new();
}

public class DashboardCountDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DashboardSourceDto
{
    public string Source { get; set; } = string.Empty;
    public int TotalCandidates { get; set; }
    public int ShortlistedCandidates { get; set; }
    public int OfferedCandidates { get; set; }
    public int JoinedCandidates { get; set; }
}

public class DashboardRequisitionDto
{
    public int RequestId { get; set; }
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public string? Priority { get; set; }
    public string HrStatus { get; set; } = string.Empty;
    public int? NumberOfPosition { get; set; }
    public int CandidateCount { get; set; }
    public int DaysOpen { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RequiredByDate { get; set; }
}

public class DashboardCandidateDto
{
    public int CandidateId { get; set; }
    public int RequisitionId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Source { get; set; }
    public string CurrentStage { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;
    public int? AssignedHRId { get; set; }
    public string? AssignedHRName { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public int DaysInPipeline { get; set; }
    public int? LatestScore { get; set; }
}

public class DashboardActivityDto
{
    public int CandidateId { get; set; }
    public string? CandidateName { get; set; }
    public int? RequisitionId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? TotalScore { get; set; }
    public string? Remarks { get; set; }
    public DateTime ActionDate { get; set; }
    public int? PerformedById { get; set; }
    public string? PerformedByName { get; set; }
}






