using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HrReportsController : ControllerBase
{
    private readonly AppDbContext _db;

    public HrReportsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Full HR activity report for a single hiring request: response time,
    /// sourcing effort, candidate pipeline, calls/follow-ups, interviews,
    /// candidate scores and a complete activity timeline.
    /// </summary>
    [HttpGet("hiring-request/{requestId:int}")]
    [ProducesResponseType(typeof(ApiResponse<HiringRequestReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HiringRequestReportDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<HiringRequestReportDto>>> GetHiringRequestReport(int requestId)
    {
        var request = await _db.HiringRequests
            .FirstOrDefaultAsync(h => h.RequestId == requestId && !h.IsDeleted);

        if (request is null)
            return NotFound(ApiResponse<HiringRequestReportDto>.Fail("Hiring request not found."));

        var sourcings = await _db.Sourcings
            .Where(s => s.HiringRequestId == requestId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var candidates = await _db.Candidates
            .Where(c => c.RequisitionId == requestId && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var candidateIds = candidates.Select(c => c.CandidateId).ToList();

        var activities = candidateIds.Count == 0
            ? new List<CandidateActivity>()
            : await _db.CandidateActivities
                .Where(a => candidateIds.Contains(a.CandidateId))
                .OrderByDescending(a => a.ActionDate)
                .ToListAsync();

        var userNames = await ResolveUserNames(activities, candidates);

        var candidatesById = candidates.ToDictionary(c => c.CandidateId);
        var timeline = MapActivities(activities, candidatesById, userNames);

        var report = new HiringRequestReportDto
        {
            RequestId = request.RequestId,
            Department = request.Department,
            Designation = request.Designation,
            NumberOfPosition = request.NumberOfPosition,
            Priority = request.Priority,
            HrStatus = request.HrStatus,
            RequestRaisedAt = request.CreatedAt,
            HrAcceptedAt = request.HrAcceptDate,
            HrResponseTime = BuildHrResponseTime(request.CreatedAt, request.HrAcceptDate),
            Sourcing = BuildSourcingSummary(sourcings),
            CandidatePipeline = BuildCandidatePipelineSummary(candidates),
            CallFollowUps = BuildCallFollowUpSummary(timeline, candidates),
            Interviews = BuildInterviewSummary(timeline),
            CandidateScores = BuildCandidateScoreSummary(timeline),
            ActivityTimeline = timeline
        };

        return Ok(ApiResponse<HiringRequestReportDto>.Ok(report, "Hiring request report generated successfully."));
    }

    /// <summary>
    /// Org-wide HR activity report for a calendar week (Monday to Sunday).
    /// Pass any date that falls inside the target week; optionally filter
    /// candidate/activity level sections to a single HR via hrId.
    /// </summary>
    [HttpGet("weekly")]
    [ProducesResponseType(typeof(ApiResponse<WeeklyHrReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<WeeklyHrReportDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<WeeklyHrReportDto>>> GetWeeklyReport(
        [FromQuery] DateOnly? weekStartDate,
        [FromQuery] int? hrId)
    {
        if (hrId.HasValue && !await _db.Users.AnyAsync(u => u.Id == hrId))
            return BadRequest(ApiResponse<WeeklyHrReportDto>.Fail("HR user not found."));

        var weekStart = GetWeekStart(weekStartDate);
        var rangeStart = DateTime.SpecifyKind(weekStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var rangeEnd = rangeStart.AddDays(7);

        var acceptedRequests = await _db.HiringRequests
            .Where(h => !h.IsDeleted && h.HrAcceptDate >= rangeStart && h.HrAcceptDate < rangeEnd)
            .OrderBy(h => h.HrAcceptDate)
            .ToListAsync();

        var sourcings = await _db.Sourcings
            .Where(s => !s.IsDeleted && s.CreatedAt >= rangeStart && s.CreatedAt < rangeEnd)
            .ToListAsync();

        var candidatesQuery = _db.Candidates.Where(c => !c.IsDeleted);
        if (hrId.HasValue)
            candidatesQuery = candidatesQuery.Where(c => c.AssignedHRId == hrId);

        var newCandidates = await candidatesQuery
            .Where(c => c.CreatedAt >= rangeStart && c.CreatedAt < rangeEnd)
            .ToListAsync();

        var shortlistedCandidates = await candidatesQuery
            .Where(c => c.ShortlistingDate != null && c.ShortlistingDate >= rangeStart && c.ShortlistingDate < rangeEnd)
            .ToListAsync();

        var activitiesQuery = _db.CandidateActivities
            .Where(a => a.ActionDate >= rangeStart && a.ActionDate < rangeEnd);
        if (hrId.HasValue)
            activitiesQuery = activitiesQuery.Where(a => a.CreatedBy == hrId);

        var activities = await activitiesQuery.OrderByDescending(a => a.ActionDate).ToListAsync();

        var relevantCandidateIds = activities.Select(a => a.CandidateId)
            .Concat(newCandidates.Select(c => c.CandidateId))
            .Concat(shortlistedCandidates.Select(c => c.CandidateId))
            .Distinct()
            .ToList();

        var relevantCandidates = relevantCandidateIds.Count == 0
            ? new List<Candidate>()
            : await _db.Candidates.Where(c => relevantCandidateIds.Contains(c.CandidateId)).ToListAsync();

        var candidatesById = relevantCandidates.ToDictionary(c => c.CandidateId);
        var userNames = await ResolveUserNames(activities, relevantCandidates);
        var timeline = MapActivities(activities, candidatesById, userNames);

        var hrName = hrId.HasValue && userNames.TryGetValue(hrId.Value, out var name) ? name : null;
        var onboardingCandidateIds = activities.Where(IsOnboardingActivity).Select(a => a.CandidateId).Distinct().ToHashSet();
        var onboardingCompletedCandidateIds = activities.Where(IsOnboardingCompletedActivity).Select(a => a.CandidateId).Distinct().ToHashSet();

        var report = new WeeklyHrReportDto
        {
            WeekStart = rangeStart,
            WeekEnd = rangeEnd.AddTicks(-1),
            HrId = hrId,
            HrName = hrName,
            RequestsAccepted = acceptedRequests.Select(h => new HrAcceptanceDto
            {
                RequestId = h.RequestId,
                Department = h.Department,
                Designation = h.Designation,
                RequestRaisedAt = h.CreatedAt,
                HrAcceptedAt = h.HrAcceptDate!.Value,
                HoursToAccept = Math.Round((h.HrAcceptDate!.Value - h.CreatedAt).TotalHours, 2)
            }).ToList(),
            Sourcing = BuildSourcingSummary(sourcings),
            NewCandidates = BuildCandidatePipelineSummary(newCandidates),
            ShortlistedThisWeek = shortlistedCandidates.Count,
            OnboardingCandidatesThisWeek = onboardingCandidateIds.Count,
            OnboardingCompletedThisWeek = onboardingCompletedCandidateIds.Count,
            CallFollowUps = BuildCallFollowUpSummary(timeline, relevantCandidates),
            Interviews = BuildInterviewSummary(timeline),
            CandidateScores = BuildCandidateScoreSummary(timeline),
            DailyBreakdown = BuildDailyBreakdown(weekStart, timeline),
            ActivityTimeline = timeline
        };

        return Ok(ApiResponse<WeeklyHrReportDto>.Ok(report, "Weekly HR report generated successfully."));
    }

    // ── Shared builders ──────────────────────────────────────────────────────

    private async Task<Dictionary<int, string>> ResolveUserNames(List<CandidateActivity> activities, List<Candidate> candidates)
    {
        var userIds = activities.Select(a => a.CreatedBy)
            .Concat(candidates.Select(c => c.AssignedHRId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
            return new Dictionary<int, string>();

        return await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());
    }

    private static List<ActivityTimelineItemDto> MapActivities(
        List<CandidateActivity> activities,
        Dictionary<int, Candidate> candidatesById,
        Dictionary<int, string> userNames)
    {
        return activities.Select(a =>
        {
            candidatesById.TryGetValue(a.CandidateId, out var candidate);
            var performedBy = a.CreatedBy.HasValue && userNames.TryGetValue(a.CreatedBy.Value, out var name)
                ? name
                : null;

            return new ActivityTimelineItemDto
            {
                CandidateId = a.CandidateId,
                CandidateName = candidate?.FullName,
                RequisitionId = candidate?.RequisitionId,
                ActivityType = a.ActivityType,
                Stage = a.Stage,
                Status = a.Status,
                TotalScore = a.TotalScore,
                Remarks = a.Remarks,
                ActionDate = a.ActionDate,
                PerformedBy = performedBy
            };
        }).ToList();
    }

    private static HrResponseTimeDto BuildHrResponseTime(DateTime raisedAt, DateTime? acceptedAt)
    {
        if (!acceptedAt.HasValue)
            return new HrResponseTimeDto { IsAccepted = false };

        var span = acceptedAt.Value - raisedAt;
        return new HrResponseTimeDto
        {
            IsAccepted = true,
            HoursToAccept = Math.Round(span.TotalHours, 2),
            FormattedDuration = FormatDuration(span)
        };
    }

    private static string FormatDuration(TimeSpan span)
    {
        if (span.TotalDays >= 1)
            return $"{(int)span.TotalDays}d {span.Hours}h {span.Minutes}m";
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours}h {span.Minutes}m";
        return $"{span.Minutes}m";
    }

    private static SourcingSummaryDto BuildSourcingSummary(List<Sourcing> sourcings)
    {
        var tasks = sourcings.Select(s => new SourcingTaskDto
        {
            SourcingId = s.SourcingId,
            HiringRequestId = s.HiringRequestId,
            Source = s.Source,
            SubSource = s.SubSource,
            Status = s.Status,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            DurationHours = s.StartTime.HasValue && s.EndTime.HasValue
                ? Math.Round((s.EndTime.Value - s.StartTime.Value).TotalHours, 2)
                : null,
            TaskDetails = s.TaskDetails,
            CreatedAt = s.CreatedAt
        }).ToList();

        var byChannel = sourcings
            .GroupBy(s => string.IsNullOrWhiteSpace(s.Source) ? "Unspecified" : s.Source!.Trim())
            .Select(g => new SourcingChannelBreakdownDto
            {
                Channel = g.Key,
                TotalTasks = g.Count(),
                CompletedTasks = g.Count(s => IsCompletedStatus(s.Status)),
                PendingTasks = g.Count(s => IsPendingStatus(s.Status))
            })
            .OrderByDescending(c => c.TotalTasks)
            .ToList();

        return new SourcingSummaryDto
        {
            TotalTasks = sourcings.Count,
            CompletedTasks = sourcings.Count(s => IsCompletedStatus(s.Status)),
            PendingTasks = sourcings.Count(s => IsPendingStatus(s.Status)),
            InProgressTasks = sourcings.Count(s => !IsCompletedStatus(s.Status) && !IsPendingStatus(s.Status)),
            ByChannel = byChannel,
            Tasks = tasks
        };
    }

    private static CandidatePipelineSummaryDto BuildCandidatePipelineSummary(List<Candidate> candidates)
    {
        return new CandidatePipelineSummaryDto
        {
            TotalCandidates = candidates.Count,
            ShortlistedCount = candidates.Count(c => c.ShortlistingDate.HasValue),
            RejectedCount = candidates.Count(c => ContainsAny(c.CurrentStage, "reject") || ContainsAny(c.CurrentStatus, "reject")),
            OfferedCount = candidates.Count(c => ContainsAny(c.CurrentStage, "offer")),
            JoinedCount = candidates.Count(c => ContainsAny(c.CurrentStage, "join")),
            OnboardingCount = candidates.Count(IsOnboardingCandidate),
            OnboardingCompletedCount = candidates.Count(IsOnboardingCompletedCandidate),
            ByStage = candidates.GroupBy(c => c.CurrentStage)
                .Select(g => new StageCountDto { Name = g.Key, Count = g.Count() })
                .OrderByDescending(s => s.Count)
                .ToList(),
            ByStatus = candidates.GroupBy(c => c.CurrentStatus)
                .Select(g => new StageCountDto { Name = g.Key, Count = g.Count() })
                .OrderByDescending(s => s.Count)
                .ToList(),
            Candidates = candidates.Select(c => new CandidateSummaryDto
            {
                CandidateId = c.CandidateId,
                FullName = c.FullName,
                Source = c.Source,
                CurrentStage = c.CurrentStage,
                CurrentStatus = c.CurrentStatus,
                CreatedAt = c.CreatedAt,
                ShortlistingDate = c.ShortlistingDate
            }).ToList()
        };
    }

    private static CallFollowUpSummaryDto BuildCallFollowUpSummary(List<ActivityTimelineItemDto> timeline, List<Candidate> candidates)
    {
        var callItems = timeline.Where(IsCallOrFollowUpActivity).ToList();
        var now = DateTime.UtcNow;

        return new CallFollowUpSummaryDto
        {
            TotalCallsAndFollowUps = callItems.Count,
            PendingFollowUps = candidates.Count(c => c.NextFollowUpDate.HasValue && c.NextFollowUpDate.Value >= now),
            Details = callItems
        };
    }

    private static InterviewSummaryDto BuildInterviewSummary(List<ActivityTimelineItemDto> timeline)
    {
        var interviewItems = timeline.Where(IsInterviewActivity).ToList();
        var scheduled = interviewItems.Count(i => IsScheduledStatus(i.Status));

        return new InterviewSummaryDto
        {
            TotalScheduled = scheduled,
            TotalCompleted = interviewItems.Count - scheduled,
            Details = interviewItems
        };
    }

    private static CandidateScoreSummaryDto BuildCandidateScoreSummary(List<ActivityTimelineItemDto> timeline)
    {
        var scored = timeline.Where(i => i.TotalScore.HasValue).ToList();

        return new CandidateScoreSummaryDto
        {
            AverageScore = scored.Count > 0 ? Math.Round(scored.Average(s => s.TotalScore!.Value), 2) : null,
            HighestScore = scored.Count > 0 ? scored.Max(s => s.TotalScore) : null,
            LowestScore = scored.Count > 0 ? scored.Min(s => s.TotalScore) : null,
            Scores = scored.Select(s => new CandidateScoreDto
            {
                CandidateId = s.CandidateId,
                CandidateName = s.CandidateName,
                Stage = s.Stage,
                TotalScore = s.TotalScore!.Value,
                ActionDate = s.ActionDate
            }).ToList()
        };
    }

    private static List<DailyActivityCountDto> BuildDailyBreakdown(DateOnly weekStart, List<ActivityTimelineItemDto> timeline)
    {
        return Enumerable.Range(0, 7)
            .Select(weekStart.AddDays)
            .Select(day =>
            {
                var dayItems = timeline.Where(a => DateOnly.FromDateTime(a.ActionDate) == day).ToList();
                return new DailyActivityCountDto
                {
                    Date = day,
                    TotalActivities = dayItems.Count,
                    CallsAndFollowUps = dayItems.Count(IsCallOrFollowUpActivity),
                    Interviews = dayItems.Count(IsInterviewActivity),
                    Shortlisted = dayItems.Count(i => ContainsAny(i.Status, "shortlist") || ContainsAny(i.Stage, "shortlist"))
                };
            })
            .ToList();
    }

    private static DateOnly GetWeekStart(DateOnly? input)
    {
        var date = input ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }

    // ── Free-text classification helpers ────────────────────────────────────
    // ActivityType/Stage/Status are free-text fields set by the UI, so HR
    // recruiter terminology is matched via keywords rather than a fixed enum.

    private static bool IsCallOrFollowUpActivity(ActivityTimelineItemDto a) =>
        ContainsAny(a.ActivityType, "call", "followup", "follow-up", "telephonic") ||
        ContainsAny(a.Stage, "call", "followup", "follow-up", "telephonic");

    private static bool IsInterviewActivity(ActivityTimelineItemDto a) =>
        ContainsAny(a.ActivityType, "interview") || ContainsAny(a.Stage, "interview");

    private static bool IsOnboardingActivity(CandidateActivity activity) =>
        ContainsAny(activity.Stage, "onboarding") || ContainsAny(activity.ActivityType, "onboarding");

    private static bool IsOnboardingCompletedActivity(CandidateActivity activity) =>
        ContainsAny(activity.ActivityType, "onboardingcompleted") ||
        (ContainsAny(activity.Stage, "onboarding") && ContainsAny(activity.Status, "complet") && !ContainsAny(activity.ActivityType, "day"));

    private static bool IsOnboardingCandidate(Candidate candidate) =>
        ContainsAny(candidate.CurrentStage, "onboarding") ||
        ContainsAny(candidate.CurrentStatus, "onboarding", "stage 1", "stage 2", "assessment submitted", "assessment review") ||
        IsOnboardingCompletedCandidate(candidate);

    private static bool IsOnboardingCompletedCandidate(Candidate candidate) =>
        ContainsAny(candidate.CurrentStage, "join") && ContainsAny(candidate.CurrentStatus, "complet");
    private static bool IsScheduledStatus(string status) =>
        ContainsAny(status, "schedul", "planned", "upcoming");

    private static bool IsCompletedStatus(string status) =>
        ContainsAny(status, "complet", "done");

    private static bool IsPendingStatus(string status) =>
        ContainsAny(status, "pending");

    private static bool ContainsAny(string? value, params string[] keywords) =>
        !string.IsNullOrWhiteSpace(value) && keywords.Any(k => value.Contains(k, StringComparison.OrdinalIgnoreCase));
}

