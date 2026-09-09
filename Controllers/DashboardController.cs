using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Recruiter dashboard quick view: workload, pipeline health, urgent actions,
    /// source performance, aging requisitions, top candidates and recent activity.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<DashboardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DashboardDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<DashboardDto>>> GetDashboard(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int? hrId)
    {
        if (hrId.HasValue && !await _db.Users.AnyAsync(u => u.Id == hrId))
            return BadRequest(ApiResponse<DashboardDto>.Fail("HR user not found."));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = fromDate ?? today.AddDays(-30);
        var to = toDate ?? today;

        if (to < from)
            return BadRequest(ApiResponse<DashboardDto>.Fail("toDate must be greater than or equal to fromDate."));

        var rangeStart = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var rangeEnd = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var now = DateTime.UtcNow;
        var todayStart = DateTime.SpecifyKind(today.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var tomorrowStart = todayStart.AddDays(1);
        var upcomingLimit = todayStart.AddDays(7);
        var staleLimit = now.AddDays(-7);

        var hiringRequests = await _db.HiringRequests
            .Where(h => !h.IsDeleted)
            .ToListAsync();

        var candidatesQuery = _db.Candidates.Where(c => !c.IsDeleted);
        if (hrId.HasValue)
            candidatesQuery = candidatesQuery.Where(c => c.AssignedHRId == hrId);

        var candidates = await candidatesQuery.ToListAsync();
        var candidateIds = candidates.Select(c => c.CandidateId).ToList();

        var activities = candidateIds.Count == 0
            ? new List<CandidateActivity>()
            : await _db.CandidateActivities
                .Where(a => candidateIds.Contains(a.CandidateId))
                .OrderByDescending(a => a.ActionDate)
                .ToListAsync();

        var candidateIdsByRequest = candidates
            .GroupBy(c => c.RequisitionId)
            .ToDictionary(g => g.Key, g => g.Count());

        var userNames = await ResolveUserNames(candidates, activities, hrId);
        var latestScores = activities
            .Where(a => a.TotalScore.HasValue)
            .GroupBy(a => a.CandidateId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.ActionDate).First().TotalScore);

        var rangeCandidates = candidates
            .Where(c => c.CreatedAt >= rangeStart && c.CreatedAt < rangeEnd)
            .ToList();

        var openRequests = hiringRequests
            .Where(h => !IsClosedRequest(h.HrStatus))
            .ToList();

        var offeredCount = candidates.Count(c => ContainsAny(c.CurrentStage, "offer") || ContainsAny(c.CurrentStatus, "offer"));
        var joinedCount = candidates.Count(c => ContainsAny(c.CurrentStage, "join") || ContainsAny(c.CurrentStatus, "join"));
        var shortlistedCount = candidates.Count(c => c.ShortlistingDate.HasValue || ContainsAny(c.CurrentStage, "shortlist") || ContainsAny(c.CurrentStatus, "shortlist"));
        var onboardingCandidateIds = candidates.Where(IsOnboardingCandidate).Select(c => c.CandidateId)
            .Concat(activities.Where(IsOnboardingActivity).Select(a => a.CandidateId)).Distinct().ToHashSet();
        var onboardingCompletedCandidateIds = candidates.Where(IsOnboardingCompletedCandidate).Select(c => c.CandidateId)
            .Concat(activities.Where(IsOnboardingCompletedActivity).Select(a => a.CandidateId)).Distinct().ToHashSet();

        var recentActivities = activities
            .Where(a => a.ActionDate >= rangeStart && a.ActionDate < rangeEnd)
            .Take(20)
            .Select(a => MapActivity(a, candidates, userNames))
            .ToList();

        var upcomingInterviewActivities = activities
            .Where(a => IsInterviewActivity(a) && IsScheduledStatus(a.Status) && a.ActionDate >= todayStart && a.ActionDate < upcomingLimit)
            .OrderBy(a => a.ActionDate)
            .Take(10)
            .Select(a => MapActivity(a, candidates, userNames))
            .ToList();

        var dashboard = new DashboardDto
        {
            FromDate = rangeStart,
            ToDate = rangeEnd.AddTicks(-1),
            HrId = hrId,
            HrName = hrId.HasValue && userNames.TryGetValue(hrId.Value, out var hrName) ? hrName : null,
            Overview = new DashboardOverviewDto
            {
                TotalHiringRequests = hiringRequests.Count,
                OpenHiringRequests = openRequests.Count,
                PendingHrAcceptance = hiringRequests.Count(h => ContainsAny(h.HrStatus, "pending")),
                PendingDirectorApproval = hiringRequests.Count(h => h.IsApprovedByDirector != true),
                TotalOpenPositions = openRequests.Sum(h => h.NumberOfPosition ?? 0),
                TotalCandidates = candidates.Count,
                NewCandidates = rangeCandidates.Count,
                ShortlistedCandidates = shortlistedCount,
                OfferedCandidates = offeredCount,
                JoinedCandidates = joinedCount,
                OnboardingCandidates = onboardingCandidateIds.Count,
                OnboardingCompletedCandidates = onboardingCompletedCandidateIds.Count,
                RejectedCandidates = candidates.Count(c => ContainsAny(c.CurrentStage, "reject") || ContainsAny(c.CurrentStatus, "reject")),
                OfferToJoinPercentage = Percentage(joinedCount, offeredCount),
                CandidateToJoinPercentage = Percentage(joinedCount, candidates.Count)
            },
            Pipeline = new DashboardPipelineDto
            {
                ByStage = BuildCounts(candidates, c => c.CurrentStage),
                ByStatus = BuildCounts(candidates, c => c.CurrentStatus),
                OnboardingProgress = BuildOnboardingProgress(candidates, activities),
                ByPriority = BuildCounts(openRequests, h => h.Priority),
                ByDepartment = BuildCounts(openRequests, h => h.Department)
            },
            RecruiterActions = new DashboardRecruiterActionDto
            {
                OverdueFollowUps = candidates.Count(c => c.NextFollowUpDate.HasValue && c.NextFollowUpDate.Value < todayStart && !IsFinalCandidate(c)),
                DueTodayFollowUps = candidates.Count(c => c.NextFollowUpDate.HasValue && c.NextFollowUpDate.Value >= todayStart && c.NextFollowUpDate.Value < tomorrowStart && !IsFinalCandidate(c)),
                UpcomingFollowUps = candidates.Count(c => c.NextFollowUpDate.HasValue && c.NextFollowUpDate.Value >= tomorrowStart && c.NextFollowUpDate.Value < upcomingLimit && !IsFinalCandidate(c)),
                InterviewsToday = activities.Count(a => IsInterviewActivity(a) && a.ActionDate >= todayStart && a.ActionDate < tomorrowStart),
                UpcomingInterviews = upcomingInterviewActivities.Count,
                StaleCandidates = candidates.Count(c => !IsFinalCandidate(c) && (c.LastActivityDate ?? c.CreatedAt) < staleLimit),
                OverdueFollowUpCandidates = candidates
                    .Where(c => c.NextFollowUpDate.HasValue && c.NextFollowUpDate.Value < todayStart && !IsFinalCandidate(c))
                    .OrderBy(c => c.NextFollowUpDate)
                    .Take(10)
                    .Select(c => MapCandidate(c, userNames, latestScores))
                    .ToList(),
                TodayFollowUpCandidates = candidates
                    .Where(c => c.NextFollowUpDate.HasValue && c.NextFollowUpDate.Value >= todayStart && c.NextFollowUpDate.Value < tomorrowStart && !IsFinalCandidate(c))
                    .OrderBy(c => c.NextFollowUpDate)
                    .Take(10)
                    .Select(c => MapCandidate(c, userNames, latestScores))
                    .ToList(),
                UpcomingInterviewDetails = upcomingInterviewActivities
            },
            SourcePerformance = BuildSourcePerformance(candidates),
            AgingRequisitions = openRequests
                .OrderByDescending(h => (now - h.CreatedAt).TotalDays)
                .Take(10)
                .Select(h => new DashboardRequisitionDto
                {
                    RequestId = h.RequestId,
                    Department = h.Department,
                    Designation = h.Designation,
                    Priority = h.Priority,
                    HrStatus = h.HrStatus,
                    NumberOfPosition = h.NumberOfPosition,
                    CandidateCount = candidateIdsByRequest.GetValueOrDefault(h.RequestId),
                    DaysOpen = Math.Max(0, (int)(now - h.CreatedAt).TotalDays),
                    CreatedAt = h.CreatedAt,
                    RequiredByDate = h.RequiredByDate
                })
                .ToList(),
            TopCandidates = candidates
                .Where(c => latestScores.ContainsKey(c.CandidateId))
                .OrderByDescending(c => latestScores[c.CandidateId])
                .ThenByDescending(c => c.LastActivityDate ?? c.CreatedAt)
                .Take(10)
                .Select(c => MapCandidate(c, userNames, latestScores))
                .ToList(),
            RecentActivity = recentActivities
        };

        return Ok(ApiResponse<DashboardDto>.Ok(dashboard, "Dashboard data generated successfully."));
    }

    private async Task<Dictionary<int, string>> ResolveUserNames(List<Candidate> candidates, List<CandidateActivity> activities, int? hrId)
    {
        var userIds = candidates.Select(c => c.AssignedHRId)
            .Concat(activities.Select(a => a.CreatedBy))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        if (hrId.HasValue)
            userIds.Add(hrId.Value);

        userIds = userIds.Distinct().ToList();

        if (userIds.Count == 0)
            return new Dictionary<int, string>();

        return await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());
    }

    private static List<DashboardCountDto> BuildCounts<T>(List<T> items, Func<T, string?> selector)
    {
        return items
            .GroupBy(item => string.IsNullOrWhiteSpace(selector(item)) ? "Unspecified" : selector(item)!.Trim())
            .Select(g => new DashboardCountDto { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private static List<DashboardSourceDto> BuildSourcePerformance(List<Candidate> candidates)
    {
        return candidates
            .GroupBy(c => string.IsNullOrWhiteSpace(c.Source) ? "Unspecified" : c.Source!.Trim())
            .Select(g => new DashboardSourceDto
            {
                Source = g.Key,
                TotalCandidates = g.Count(),
                ShortlistedCandidates = g.Count(c => c.ShortlistingDate.HasValue || ContainsAny(c.CurrentStage, "shortlist") || ContainsAny(c.CurrentStatus, "shortlist")),
                OfferedCandidates = g.Count(c => ContainsAny(c.CurrentStage, "offer") || ContainsAny(c.CurrentStatus, "offer")),
                JoinedCandidates = g.Count(c => ContainsAny(c.CurrentStage, "join") || ContainsAny(c.CurrentStatus, "join"))
            })
            .OrderByDescending(s => s.TotalCandidates)
            .Take(10)
            .ToList();
    }

    private static DashboardCandidateDto MapCandidate(
        Candidate candidate,
        Dictionary<int, string> userNames,
        Dictionary<int, int?> latestScores)
    {
        var assignedHRName = candidate.AssignedHRId.HasValue && userNames.TryGetValue(candidate.AssignedHRId.Value, out var name)
            ? name
            : null;

        return new DashboardCandidateDto
        {
            CandidateId = candidate.CandidateId,
            RequisitionId = candidate.RequisitionId,
            FullName = candidate.FullName,
            Email = candidate.Email,
            PhoneNumber = candidate.PhoneNumber,
            Source = candidate.Source,
            CurrentStage = candidate.CurrentStage,
            CurrentStatus = candidate.CurrentStatus,
            AssignedHRId = candidate.AssignedHRId,
            AssignedHRName = assignedHRName,
            NextFollowUpDate = candidate.NextFollowUpDate,
            LastActivityDate = candidate.LastActivityDate,
            DaysInPipeline = Math.Max(0, (int)(DateTime.UtcNow - candidate.CreatedAt).TotalDays),
            LatestScore = latestScores.GetValueOrDefault(candidate.CandidateId)
        };
    }

    private static DashboardActivityDto MapActivity(
        CandidateActivity activity,
        List<Candidate> candidates,
        Dictionary<int, string> userNames)
    {
        var candidate = candidates.FirstOrDefault(c => c.CandidateId == activity.CandidateId);
        var performedByName = activity.CreatedBy.HasValue && userNames.TryGetValue(activity.CreatedBy.Value, out var name)
            ? name
            : null;

        return new DashboardActivityDto
        {
            CandidateId = activity.CandidateId,
            CandidateName = candidate?.FullName,
            RequisitionId = candidate?.RequisitionId,
            ActivityType = activity.ActivityType,
            Stage = activity.Stage,
            Status = activity.Status,
            TotalScore = activity.TotalScore,
            Remarks = activity.Remarks,
            ActionDate = activity.ActionDate,
            PerformedById = activity.CreatedBy,
            PerformedByName = performedByName
        };
    }

    private static List<DashboardCountDto> BuildOnboardingProgress(List<Candidate> candidates, List<CandidateActivity> activities)
    {
        var candidateIds = candidates.Select(c => c.CandidateId).ToHashSet();
        return activities.Where(a => candidateIds.Contains(a.CandidateId) && IsOnboardingActivity(a))
            .GroupBy(a => string.IsNullOrWhiteSpace(a.Status) ? "Unspecified" : a.Status.Trim())
            .Select(g => new DashboardCountDto { Name = g.Key, Count = g.Select(a => a.CandidateId).Distinct().Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name).ToList();
    }

    private static bool IsOnboardingCandidate(Candidate candidate) =>
        ContainsAny(candidate.CurrentStage, "onboarding") ||
        ContainsAny(candidate.CurrentStatus, "onboarding", "stage 1", "stage 2", "assessment submitted", "assessment review") ||
        IsOnboardingCompletedCandidate(candidate);

    private static bool IsOnboardingCompletedCandidate(Candidate candidate) =>
        ContainsAny(candidate.CurrentStage, "join") && ContainsAny(candidate.CurrentStatus, "complet");

    private static bool IsOnboardingActivity(CandidateActivity activity) =>
        ContainsAny(activity.Stage, "onboarding") || ContainsAny(activity.ActivityType, "onboarding");

    private static bool IsOnboardingCompletedActivity(CandidateActivity activity) =>
        ContainsAny(activity.ActivityType, "onboardingcompleted") ||
        (ContainsAny(activity.Stage, "onboarding") && ContainsAny(activity.Status, "complet") && !ContainsAny(activity.ActivityType, "day"));
    private static decimal Percentage(int value, int total)
    {
        return total == 0 ? 0 : Math.Round((decimal)value * 100 / total, 2);
    }

    private static bool IsFinalCandidate(Candidate candidate) =>
        ContainsAny(candidate.CurrentStage, "reject", "join") ||
        ContainsAny(candidate.CurrentStatus, "reject", "join");

    private static bool IsClosedRequest(string? status) =>
        ContainsAny(status, "closed", "cancel", "reject", "filled", "complete");

    private static bool IsInterviewActivity(CandidateActivity activity) =>
        ContainsAny(activity.ActivityType, "interview") || ContainsAny(activity.Stage, "interview");

    private static bool IsScheduledStatus(string status) =>
        ContainsAny(status, "schedul", "planned", "upcoming");

    private static bool ContainsAny(string? value, params string[] keywords) =>
        !string.IsNullOrWhiteSpace(value) && keywords.Any(k => value.Contains(k, StringComparison.OrdinalIgnoreCase));
}

