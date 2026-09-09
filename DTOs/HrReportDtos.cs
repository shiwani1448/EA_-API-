using System.Text.Json;

namespace hrms_api.DTOs;

// ── Report 1: per-requisition HR activity report ───────────────────────────

public class HiringRequestReportDto
{
    public int RequestId { get; set; }
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public int? NumberOfPosition { get; set; }
    public string? Priority { get; set; }
    public string HrStatus { get; set; } = string.Empty;
    public DateTime RequestRaisedAt { get; set; }
    public DateTime? HrAcceptedAt { get; set; }
    public HrResponseTimeDto HrResponseTime { get; set; } = new();

    public SourcingSummaryDto Sourcing { get; set; } = new();
    public CandidatePipelineSummaryDto CandidatePipeline { get; set; } = new();
    public CallFollowUpSummaryDto CallFollowUps { get; set; } = new();
    public InterviewSummaryDto Interviews { get; set; } = new();
    public CandidateScoreSummaryDto CandidateScores { get; set; } = new();
    public List<ActivityTimelineItemDto> ActivityTimeline { get; set; } = new();
}

// ── Report 2: weekly HR activity report ─────────────────────────────────────

public class WeeklyHrReportDto
{
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public int? HrId { get; set; }
    public string? HrName { get; set; }

    public List<HrAcceptanceDto> RequestsAccepted { get; set; } = new();
    public SourcingSummaryDto Sourcing { get; set; } = new();
    public CandidatePipelineSummaryDto NewCandidates { get; set; } = new();
    public int ShortlistedThisWeek { get; set; }
    public int OnboardingCandidatesThisWeek { get; set; }
    public int OnboardingCompletedThisWeek { get; set; }
    public CallFollowUpSummaryDto CallFollowUps { get; set; } = new();
    public InterviewSummaryDto Interviews { get; set; } = new();
    public CandidateScoreSummaryDto CandidateScores { get; set; } = new();
    public List<DailyActivityCountDto> DailyBreakdown { get; set; } = new();
    public List<ActivityTimelineItemDto> ActivityTimeline { get; set; } = new();
}

public class HrAcceptanceDto
{
    public int RequestId { get; set; }
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public DateTime RequestRaisedAt { get; set; }
    public DateTime HrAcceptedAt { get; set; }
    public double HoursToAccept { get; set; }
}

public class DailyActivityCountDto
{
    public DateOnly Date { get; set; }
    public int TotalActivities { get; set; }
    public int CallsAndFollowUps { get; set; }
    public int Interviews { get; set; }
    public int Shortlisted { get; set; }
}

// ── Shared building blocks ──────────────────────────────────────────────────

public class HrResponseTimeDto
{
    public bool IsAccepted { get; set; }
    public double? HoursToAccept { get; set; }
    public string? FormattedDuration { get; set; }
}

public class SourcingSummaryDto
{
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int PendingTasks { get; set; }
    public int InProgressTasks { get; set; }
    public List<SourcingChannelBreakdownDto> ByChannel { get; set; } = new();
    public List<SourcingTaskDto> Tasks { get; set; } = new();
}

public class SourcingChannelBreakdownDto
{
    public string Channel { get; set; } = string.Empty;
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int PendingTasks { get; set; }
}

public class SourcingTaskDto
{
    public int SourcingId { get; set; }
    public int HiringRequestId { get; set; }
    public string? Source { get; set; }
    public string? SubSource { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double? DurationHours { get; set; }
    public JsonDocument? TaskDetails { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CandidatePipelineSummaryDto
{
    public int TotalCandidates { get; set; }
    public int ShortlistedCount { get; set; }
    public int RejectedCount { get; set; }
    public int OfferedCount { get; set; }
    public int JoinedCount { get; set; }
    public int OnboardingCount { get; set; }
    public int OnboardingCompletedCount { get; set; }
    public List<StageCountDto> ByStage { get; set; } = new();
    public List<StageCountDto> ByStatus { get; set; } = new();
    public List<CandidateSummaryDto> Candidates { get; set; } = new();
}

public class StageCountDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class CandidateSummaryDto
{
    public int CandidateId { get; set; }
    public string? FullName { get; set; }
    public string? Source { get; set; }
    public string CurrentStage { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ShortlistingDate { get; set; }
}

public class CallFollowUpSummaryDto
{
    public int TotalCallsAndFollowUps { get; set; }
    public int PendingFollowUps { get; set; }
    public List<ActivityTimelineItemDto> Details { get; set; } = new();
}

public class InterviewSummaryDto
{
    public int TotalScheduled { get; set; }
    public int TotalCompleted { get; set; }
    public List<ActivityTimelineItemDto> Details { get; set; } = new();
}

public class CandidateScoreSummaryDto
{
    public double? AverageScore { get; set; }
    public int? HighestScore { get; set; }
    public int? LowestScore { get; set; }
    public List<CandidateScoreDto> Scores { get; set; } = new();
}

public class CandidateScoreDto
{
    public int CandidateId { get; set; }
    public string? CandidateName { get; set; }
    public string Stage { get; set; } = string.Empty;
    public int TotalScore { get; set; }
    public DateTime ActionDate { get; set; }
}

public class ActivityTimelineItemDto
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
    public string? PerformedBy { get; set; }
}


