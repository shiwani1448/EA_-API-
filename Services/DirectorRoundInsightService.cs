using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Models;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Services;

public class DirectorRoundInsightService : IDirectorRoundInsightService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly AppDbContext _db;

    public DirectorRoundInsightService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(int StatusCode, DirectorRoundCandidateInsightResponseDto Response)> GetCandidateInsightAsync(
        int candidateId,
        int interviewRoundId,
        CancellationToken cancellationToken = default)
    {
        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.CandidateId == candidateId && !c.IsDeleted, cancellationToken);

        if (candidate is null)
            return (StatusCodes.Status404NotFound, Fail("Candidate not found."));

        var directorRound = await _db.CandidateActivities
            .FirstOrDefaultAsync(a => a.Id == interviewRoundId && a.CandidateId == candidateId, cancellationToken);

        if (directorRound is null)
            return (StatusCodes.Status404NotFound, Fail("Interview round not found."));

        if (!IsDirectorRound(directorRound))
            return (StatusCodes.Status400BadRequest, Fail("Interview round is not Director Round."));

        var hiringRequest = await _db.HiringRequests
            .FirstOrDefaultAsync(h => h.RequestId == candidate.RequisitionId && !h.IsDeleted, cancellationToken);

        JDMaster? jdMaster = null;
        if (hiringRequest?.JDID is not null)
        {
            jdMaster = await _db.JDMasters
                .FirstOrDefaultAsync(j => j.Id == hiringRequest.JDID.Value && !j.IsDeleted, cancellationToken);
        }

        var activities = await _db.CandidateActivities
            .Where(a => a.CandidateId == candidateId)
            .OrderBy(a => a.ActionDate)
            .ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);

        var screening = await _db.CandidateAIScreenings
            .Where(s => s.CandidateId == candidateId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var assessmentEvaluation = await _db.AssessmentEvaluations
            .Where(e => e.CandidateId == candidateId)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var data = new DirectorRoundCandidateInsightDataDto
        {
            Candidate = BuildCandidate(candidate, hiringRequest, jdMaster),
            CandidateDates = BuildCandidateDates(candidate, screening, directorRound),
            CandidateTimeline = BuildCandidateTimeline(activities),
            ScreeningInsight = BuildScreeningInsight(screening),
            DiscInsight = BuildDiscInsight(activities),
            HrRoundInsight = BuildHrRoundInsight(activities),
            TechnicalAssessmentInsight = BuildTechnicalAssessmentInsight(activities, assessmentEvaluation)
        };

        data.ScoreBreakdown = BuildScoreBreakdown(data);
        data.OverallAnalysis = BuildFallbackOverallAnalysis(data);
        data.DirectorQuestions = BuildFallbackQuestions(data);

        return (StatusCodes.Status200OK, new DirectorRoundCandidateInsightResponseDto
        {
            Success = true,
            Message = "Director round candidate insight generated successfully.",
            Data = data
        });
    }

    private static DirectorCandidateBasicDetailsDto BuildCandidate(
        Candidate candidate,
        HiringRequest? hiringRequest,
        JDMaster? jdMaster) => new()
    {
        CandidateId = candidate.CandidateId,
        CandidateName = candidate.FullName,
        Department = hiringRequest?.Department ?? jdMaster?.Department,
        Designation = hiringRequest?.Designation ?? jdMaster?.Designation,
        Level = InferLevel(candidate.YearsOfExperience, hiringRequest?.ExperienceRequired),
        Experience = candidate.YearsOfExperience,
        CurrentCTC = candidate.CurrentCtcLpa,
        ExpectedCTC = candidate.ExpectedCtcLpa,
        NoticePeriod = candidate.NoticePeriod,
        Source = candidate.Source,
        CurrentStatus = candidate.CurrentStatus,
        CurrentStage = candidate.CurrentStage,
        Email = candidate.Email,
        PhoneNumber = candidate.PhoneNumber,
        KeySkills = candidate.KeySkills,
        Remarks = candidate.Remarks
    };

    private static DirectorCandidateDatesDto BuildCandidateDates(
        Candidate candidate,
        CandidateAIScreening? screening,
        CandidateActivity directorRound) => new()
    {
        DateOfBirth = candidate.DateOfBirth,
        CreatedAt = candidate.CreatedAt,
        UpdatedAt = candidate.UpdatedAt,
        ShortlistingDate = candidate.ShortlistingDate,
        NextFollowUpDate = candidate.NextFollowUpDate,
        LastActivityDate = candidate.LastActivityDate,
        LatestScreeningCreatedAt = screening?.CreatedAt,
        LatestScreeningUpdatedAt = screening?.UpdatedAt,
        DirectorRoundActionDate = directorRound.ActionDate,
        DirectorRoundCreatedAt = directorRound.CreatedAt
    };

    private static List<DirectorCandidateTimelineItemDto> BuildCandidateTimeline(List<CandidateActivity> activities) =>
        activities
            .OrderBy(a => a.ActionDate)
            .ThenBy(a => a.Id)
            .Select(a => new DirectorCandidateTimelineItemDto
            {
                ActivityId = a.Id,
                ActivityType = a.ActivityType,
                Stage = a.Stage,
                Status = a.Status,
                TotalScore = a.TotalScore,
                Remarks = a.Remarks,
                ActionDate = a.ActionDate,
                CreatedAt = a.CreatedAt
            })
            .ToList();

    private static DirectorScreeningInsightDto BuildScreeningInsight(CandidateAIScreening? screening)
    {
        if (screening is null)
        {
            return new DirectorScreeningInsightDto
            {
                IsMissing = true,
                MissingReason = "Screening data not found."
            };
        }

        return new DirectorScreeningInsightDto
        {
            ScreeningScore = screening.OverallScore,
            ScreeningDecision = screening.Decision,
            ResumeMatchSummary = screening.ShortSummary,
            JDMatchSummary = screening.Recommendation,
            ScreeningStrengths = DeserializeList(screening.StrengthsJson),
            ScreeningWeaknesses = DeserializeList(screening.ConcernsJson)
                .Concat(DeserializeList(screening.MissingSkillsJson))
                .Distinct()
                .ToList(),
            ScreeningRemarks = screening.ErrorMessage ?? screening.ShortSummary,
            IsMissing = false
        };
    }

    private static DirectorDiscInsightDto BuildDiscInsight(List<CandidateActivity> activities)
    {
        var discActivity = activities
            .Where(a => IsStage(a, "Disc Round") && IsActivity(a, "InterviewCompleted") && a.EvaluationJson is not null)
            .OrderByDescending(a => a.ActionDate)
            .ThenByDescending(a => a.Id)
            .FirstOrDefault();

        var disc = ParseDisc(discActivity?.EvaluationJson);
        if (disc is null)
        {
            return new DirectorDiscInsightDto
            {
                IsMissing = true,
                MissingReason = "DISC result not found."
            };
        }

        var profile = disc.DiscProfile ?? "DISC profile";
        var dominant = GetDominantDisc(disc);

        return new DirectorDiscInsightDto
        {
            DiscProfile = disc.DiscProfile,
            DScore = disc.DScore,
            IScore = disc.IScore,
            SScore = disc.SScore,
            CScore = disc.CScore,
            PersonalitySummary = $"{profile} with dominant {dominant} behavior indicators.",
            WorkStyle = GetDiscWorkStyle(dominant),
            CommunicationStyle = GetDiscCommunicationStyle(dominant),
            StrengthBehavior = GetDiscStrength(dominant),
            RiskBehavior = GetDiscRisk(dominant),
            HowDirectorShouldHandleCandidate = GetDiscDirectorGuidance(dominant),
            IsMissing = false
        };
    }

    private static DirectorHrRoundInsightDto BuildHrRoundInsight(List<CandidateActivity> activities)
    {
        var hr = activities
            .Where(a => IsStage(a, "HR Round") && IsActivity(a, "InterviewCompleted"))
            .OrderByDescending(a => a.ActionDate)
            .ThenByDescending(a => a.Id)
            .FirstOrDefault();

        if (hr is null)
        {
            return new DirectorHrRoundInsightDto
            {
                IsMissing = true,
                MissingReason = "HR Round result not found."
            };
        }

        var json = hr.EvaluationJson?.RootElement;
        return new DirectorHrRoundInsightDto
        {
            HrRoundScore = hr.TotalScore,
            QuestionWiseHrPerformance = CloneJson(json),
            HRStrengths = GetJsonStringList(json, "strengths", "hrStrengths", "strongSignals"),
            HRConcerns = GetJsonStringList(json, "concerns", "hrConcerns", "redFlags"),
            HRRemarks = hr.Remarks ?? GetJsonString(json, "remarks", "summary", "feedback"),
            CommunicationScore = GetJsonDecimal(json, "communicationScore"),
            ConfidenceScore = GetJsonDecimal(json, "confidenceScore"),
            CultureFitScore = GetJsonDecimal(json, "cultureFitScore"),
            AttitudeScore = GetJsonDecimal(json, "attitudeScore"),
            LearningAbilityScore = GetJsonDecimal(json, "learningAbilityScore"),
            IsMissing = false
        };
    }

    private static DirectorTechnicalAssessmentInsightDto BuildTechnicalAssessmentInsight(
        List<CandidateActivity> activities,
        AssessmentEvaluation? assessmentEvaluation)
    {
        var technical = activities
            .Where(a => Contains(a.Stage, "Technical") && IsActivity(a, "InterviewCompleted"))
            .OrderByDescending(a => a.ActionDate)
            .ThenByDescending(a => a.Id)
            .FirstOrDefault();

        var assessment = activities
            .Where(a => Contains(a.Stage, "Assessment") && IsActivity(a, "InterviewCompleted"))
            .OrderByDescending(a => a.ActionDate)
            .ThenByDescending(a => a.Id)
            .FirstOrDefault();

        if (technical is null && assessment is null && assessmentEvaluation is null)
        {
            return new DirectorTechnicalAssessmentInsightDto
            {
                IsMissing = true,
                MissingReason = "Technical or Assessment result not found."
            };
        }

        var technicalJson = technical?.EvaluationJson?.RootElement;
        var assessmentJson = assessment?.EvaluationJson?.RootElement;

        return new DirectorTechnicalAssessmentInsightDto
        {
            TechnicalScore = technical?.TotalScore,
            AssessmentScore = assessment?.TotalScore ?? DecimalToNullableInt(assessmentEvaluation?.Percentage),
            AssessmentObtainedMarks = assessmentEvaluation?.ObtainedMarks,
            AssessmentTotalMarks = assessmentEvaluation?.TotalMarks,
            AssessmentPercentage = assessmentEvaluation?.Percentage,
            AssessmentResult = assessmentEvaluation?.Result,
            AssessmentCreatedAt = assessmentEvaluation?.CreatedAt,
            QuestionWiseTechnicalPerformance = CloneJson(technicalJson ?? assessmentJson),
            PracticalScore = GetJsonDecimal(assessmentJson, "practicalScore") ?? GetJsonDecimal(technicalJson, "practicalScore"),
            SkillWiseScores = CloneJson(GetJsonElement(technicalJson, "skillWiseScores", "skillScores")
                ?? GetJsonElement(assessmentJson, "skillWiseScores", "skillScores")),
            StrongSkillAreas = GetJsonStringList(technicalJson, "strongSkillAreas", "strengths")
                .Concat(GetJsonStringList(assessmentJson, "strongSkillAreas", "strengths"))
                .Distinct()
                .ToList(),
            WeakSkillAreas = GetJsonStringList(technicalJson, "weakSkillAreas", "concerns", "weaknesses")
                .Concat(GetJsonStringList(assessmentJson, "weakSkillAreas", "concerns", "weaknesses"))
                .Distinct()
                .ToList(),
            InterviewerRemarks = technical?.Remarks ?? assessment?.Remarks
                ?? GetJsonString(technicalJson, "remarks", "feedback", "summary")
                ?? GetJsonString(assessmentJson, "remarks", "feedback", "summary"),
            IsMissing = false
        };
    }

    private static DirectorOverallAnalysisDto BuildFallbackOverallAnalysis(DirectorRoundCandidateInsightDataDto data)
    {
        var overall = data.ScoreBreakdown.OverallFitScore;
        var risk = overall switch
        {
            >= 75 => "Low",
            >= 55 => "Medium",
            null => "Medium",
            _ => "High"
        };

        var recommendation = overall switch
        {
            >= 85 => "Strong Hire",
            >= 70 => "Hire",
            >= 50 => "Hold",
            null => "Hold",
            _ => "Reject"
        };

        var strengths = data.ScreeningInsight.ScreeningStrengths
            .Concat(data.HrRoundInsight.HRStrengths)
            .Concat(data.TechnicalAssessmentInsight.StrongSkillAreas)
            .Distinct()
            .ToList();

        var weaknesses = data.ScreeningInsight.ScreeningWeaknesses
            .Concat(data.HrRoundInsight.HRConcerns)
            .Concat(data.TechnicalAssessmentInsight.WeakSkillAreas)
            .Distinct()
            .ToList();

        return new DirectorOverallAnalysisDto
        {
            OverallFitScore = overall,
            RiskLevel = risk,
            FinalRecommendation = recommendation,
            CandidateStrengthSummary = strengths,
            CandidateWeaknessSummary = weaknesses,
            HiringRiskReason = weaknesses.Count > 0 ? string.Join("; ", weaknesses.Take(3)) : "No major risk found in available data.",
            RoleFitSummary = data.ScreeningInsight.ScreeningDecision ?? "Role fit should be validated by Director.",
            CultureFitSummary = data.HrRoundInsight.IsMissing ? "HR culture-fit data is missing." : "Culture fit available from HR round.",
            LearningPotential = data.HrRoundInsight.LearningAbilityScore.HasValue ? $"Learning ability score: {data.HrRoundInsight.LearningAbilityScore}" : "Learning potential requires Director validation.",
            StabilityPrediction = "Validate long-term seriousness and stability in Director Round.",
            SalaryRisk = BuildSalaryRisk(data.Candidate.CurrentCTC, data.Candidate.ExpectedCTC),
            NoticePeriodRisk = string.IsNullOrWhiteSpace(data.Candidate.NoticePeriod) ? "Notice period not available." : $"Notice period: {data.Candidate.NoticePeriod}"
        };
    }

    private static DirectorScoreBreakdownDto BuildScoreBreakdown(DirectorRoundCandidateInsightDataDto data)
    {
        var components = new List<DirectorScoreComponentDto>
        {
            NewScoreComponent(
                "AI Screening",
                data.ScreeningInsight.ScreeningScore,
                "CandidateAIScreenings.OverallScore",
                "Resume/JD match, skills, experience relevance, risk, and AI screening decision."),
            NewScoreComponent(
                "HR Round",
                data.HrRoundInsight.HrRoundScore,
                "CandidateActivities.TotalScore for HR Round InterviewCompleted",
                "Communication, confidence, attitude, culture fit, and learning ability signals from HR round."),
            NewScoreComponent(
                "Technical Round",
                data.TechnicalAssessmentInsight.TechnicalScore,
                "CandidateActivities.TotalScore for Technical InterviewCompleted",
                "Technical interview performance from stored candidate activity."),
            NewScoreComponent(
                "Assessment",
                data.TechnicalAssessmentInsight.AssessmentPercentage ?? data.TechnicalAssessmentInsight.AssessmentScore,
                data.TechnicalAssessmentInsight.AssessmentTotalMarks.HasValue
                    ? "AssessmentEvaluations.ObtainedMarks / AssessmentEvaluations.TotalMarks"
                    : "CandidateActivities.TotalScore for Assessment InterviewCompleted",
                data.TechnicalAssessmentInsight.AssessmentTotalMarks.HasValue
                    ? $"Assessment marks: {data.TechnicalAssessmentInsight.AssessmentObtainedMarks:0.##} out of {data.TechnicalAssessmentInsight.AssessmentTotalMarks:0.##}."
                    : "Assessment score from stored candidate activity.")
        };

        var included = components.Where(c => c.Score.HasValue).ToList();
        var overall = included.Count == 0 ? (decimal?)null : Math.Round(included.Average(c => c.Score!.Value), 2);
        var calculation = included.Count == 0
            ? "No score components available."
            : $"({string.Join(" + ", included.Select(c => $"{c.Score:0.##}"))}) / {included.Count} = {overall:0.##}";

        return new DirectorScoreBreakdownDto
        {
            OverallFitScore = overall,
            CalculationMethod = "Equal-weight average of available score components. Missing components are excluded from the average.",
            Calculation = calculation,
            Components = components
        };
    }

    private static DirectorScoreComponentDto NewScoreComponent(
        string name,
        decimal? score,
        string source,
        string explanation) => new()
    {
        Name = name,
        Score = score.HasValue ? Math.Round(score.Value, 2) : null,
        OutOf = 100,
        IncludedInOverall = score.HasValue,
        Source = source,
        Explanation = score.HasValue ? explanation : "Not available for this candidate."
    };

    private static List<DirectorValidationQuestionDto> BuildFallbackQuestions(DirectorRoundCandidateInsightDataDto data)
    {
        var weakArea = data.OverallAnalysis.CandidateWeaknessSummary.FirstOrDefault() ?? "role readiness";
        var strongArea = data.OverallAnalysis.CandidateStrengthSummary.FirstOrDefault() ?? "candidate strengths";
        var role = $"{data.Candidate.Department} {data.Candidate.Designation}".Trim();

        return new List<DirectorValidationQuestionDto>
        {
            NewQuestion(1, $"What makes you serious about building a long-term career in this {role} role?", "Validates role seriousness and stability.", "Clear motivation, realistic expectations, and long-term ownership.", "Generic answer or short-term salary-only motivation.", "Stability"),
            NewQuestion(2, $"Tell me about a time you owned an outcome end-to-end despite pressure or ambiguity.", "Checks ownership and maturity.", "Specific example with decision-making, follow-through, and learning.", "Blames others or cannot explain personal ownership.", "Ownership"),
            NewQuestion(3, $"How will you improve or manage your weaker area around {weakArea}?", "Validates self-awareness around identified risk.", "Accepts the gap and gives practical improvement steps.", "Denies the gap or gives vague promises.", weakArea),
            NewQuestion(4, $"Your strongest signal appears to be {strongArea}. How will you use it without overplaying it?", "Checks balanced use of strengths.", "Shows maturity, balance, and awareness of team impact.", "Overconfidence or inability to see trade-offs.", strongArea),
            NewQuestion(5, "How do you handle feedback when it is direct, urgent, or different from your own view?", "Tests coachability and pressure handling.", "Calm example of listening, adapting, and improving.", "Defensive response or resistance to feedback.", "Coachability")
        };
    }

    private static bool IsDirectorRound(CandidateActivity activity) =>
        Contains(activity.Stage, "Director") && Contains(activity.ActivityType, "InterviewScheduled");

    private static bool IsStage(CandidateActivity activity, string stage) =>
        activity.Stage.Equals(stage, StringComparison.OrdinalIgnoreCase);

    private static bool IsActivity(CandidateActivity activity, string activityType) =>
        activity.ActivityType.Equals(activityType, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string? value, string part) =>
        value?.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

    private static object? CloneJson(JsonElement? element)
    {
        if (!element.HasValue || element.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return JsonSerializer.Deserialize<object>(element.Value.GetRawText(), JsonOptions);
    }

    private static JsonElement? GetJsonElement(JsonElement? element, params string[] names)
    {
        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var name in names)
        {
            if (TryGetJsonProperty(element.Value, name, out var property))
                return property;
        }

        return null;
    }

    private static string? GetJsonString(JsonElement? element, params string[] names)
    {
        var property = GetJsonElement(element, names);
        if (!property.HasValue)
            return null;

        return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.ToString();
    }

    private static decimal? GetJsonDecimal(JsonElement? element, params string[] names)
    {
        var property = GetJsonElement(element, names);
        if (!property.HasValue)
            return null;

        if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDecimal(out var number))
            return number;

        if (property.Value.ValueKind == JsonValueKind.String &&
            decimal.TryParse(property.Value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    private static List<string> GetJsonStringList(JsonElement? element, params string[] names)
    {
        var property = GetJsonElement(element, names);
        if (!property.HasValue)
            return new List<string>();

        if (property.Value.ValueKind == JsonValueKind.Array)
        {
            return property.Value.EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToList();
        }

        var value = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.ToString();
        return string.IsNullOrWhiteSpace(value) ? new List<string>() : new List<string> { value! };
    }

    private static bool TryGetJsonProperty(JsonElement element, string name, out JsonElement property)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out property))
            return true;

        var normalized = Normalize(name);
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var item in element.EnumerateObject())
            {
                if (Normalize(item.Name) == normalized)
                {
                    property = item.Value;
                    return true;
                }
            }
        }

        property = default;
        return false;
    }

    private static DiscResult? ParseDisc(JsonDocument? json)
    {
        if (json is null)
            return null;

        var root = json.RootElement;
        var scoreElement = GetJsonElement(root, "discScore", "dISCScore", "DISCScore") ?? root;

        var disc = new DiscResult
        {
            DiscProfile = GetJsonString(root, "discProfile", "dISCProfile", "DISCProfile"),
            DScore = GetJsonDecimal(scoreElement, "D", "dScore", "DScore"),
            IScore = GetJsonDecimal(scoreElement, "I", "iScore", "IScore"),
            SScore = GetJsonDecimal(scoreElement, "S", "sScore", "SScore"),
            CScore = GetJsonDecimal(scoreElement, "C", "cScore", "CScore")
        };

        return disc.DiscProfile is not null || disc.DScore.HasValue || disc.IScore.HasValue || disc.SScore.HasValue || disc.CScore.HasValue
            ? disc
            : null;
    }

    private static List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try { return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>(); }
        catch { return new List<string>(); }
    }

    private static string Normalize(string value) =>
        Regex.Replace(value, @"[^a-z0-9]", "", RegexOptions.IgnoreCase).ToLowerInvariant();

    private static string? InferLevel(int? experienceYears, string? experienceText)
    {
        if (experienceYears.HasValue)
        {
            return experienceYears.Value switch
            {
                <= 2 => "Junior",
                <= 5 => "Mid",
                _ => "Senior"
            };
        }

        return string.IsNullOrWhiteSpace(experienceText) ? null : experienceText;
    }

    private static int? DecimalToNullableInt(decimal? value) =>
        value.HasValue ? Convert.ToInt32(Math.Round(value.Value, 0)) : null;

    private static string GetDominantDisc(DiscResult disc)
    {
        var scores = new Dictionary<string, decimal?>
        {
            ["D"] = disc.DScore,
            ["I"] = disc.IScore,
            ["S"] = disc.SScore,
            ["C"] = disc.CScore
        };

        return scores
            .Where(s => s.Value.HasValue)
            .OrderByDescending(s => s.Value)
            .Select(s => s.Key)
            .FirstOrDefault() ?? "Unknown";
    }

    private static string GetDiscWorkStyle(string dominant) => dominant switch
    {
        "D" => "Fast-paced, outcome-oriented, direct.",
        "I" => "People-oriented, expressive, collaborative.",
        "S" => "Stable, supportive, process-comfortable.",
        "C" => "Analytical, detail-focused, quality-driven.",
        _ => "Work style needs validation."
    };

    private static string GetDiscCommunicationStyle(string dominant) => dominant switch
    {
        "D" => "Direct and decisive.",
        "I" => "Persuasive and relationship-led.",
        "S" => "Calm, patient, and steady.",
        "C" => "Precise, structured, and evidence-led.",
        _ => "Communication style needs validation."
    };

    private static string GetDiscStrength(string dominant) => dominant switch
    {
        "D" => "Ownership, urgency, and decision-making.",
        "I" => "Influence, energy, and relationship building.",
        "S" => "Consistency, patience, and team support.",
        "C" => "Accuracy, discipline, and structured thinking.",
        _ => "Strength behavior needs validation."
    };

    private static string GetDiscRisk(string dominant) => dominant switch
    {
        "D" => "May appear impatient, forceful, or low on listening.",
        "I" => "May lose follow-up discipline or documentation depth.",
        "S" => "May avoid urgency, conflict, or rapid decisions.",
        "C" => "May become slow, rigid, or over-analytical under pressure.",
        _ => "Risk behavior needs validation."
    };

    private static string GetDiscDirectorGuidance(string dominant) => dominant switch
    {
        "D" => "Validate teamwork, patience, listening, and aggression control.",
        "I" => "Validate follow-up discipline, consistency, documentation, and seriousness.",
        "S" => "Validate urgency, ownership, adaptability, and decision-making.",
        "C" => "Validate flexibility, speed, pressure handling, and practical decision-making.",
        _ => "Ask balanced questions on ownership, stability, communication, and adaptability."
    };

    private static string BuildSalaryRisk(decimal? currentCtc, decimal? expectedCtc)
    {
        if (!currentCtc.HasValue || !expectedCtc.HasValue || currentCtc.Value <= 0)
            return "Salary data incomplete.";

        var hike = Math.Round((expectedCtc.Value - currentCtc.Value) * 100 / currentCtc.Value, 2);
        return hike switch
        {
            >= 50 => $"High salary jump expectation: {hike}%.",
            >= 30 => $"Moderate salary jump expectation: {hike}%.",
            _ => $"Salary expectation appears manageable: {hike}% jump."
        };
    }

    private static DirectorValidationQuestionDto NewQuestion(
        int no,
        string question,
        string why,
        string strong,
        string redFlag,
        string related) => new()
    {
        QuestionNo = no,
        Question = question,
        WhyDirectorShouldAskThis = why,
        WhatStrongAnswerLooksLike = strong,
        RedFlagAnswer = redFlag,
        RelatedRiskOrSkill = related
    };

    private static DirectorRoundCandidateInsightResponseDto Fail(string message) => new()
    {
        Success = false,
        Message = message
    };

    private sealed class DiscResult
    {
        public string? DiscProfile { get; set; }
        public decimal? DScore { get; set; }
        public decimal? IScore { get; set; }
        public decimal? SScore { get; set; }
        public decimal? CScore { get; set; }
    }

}
