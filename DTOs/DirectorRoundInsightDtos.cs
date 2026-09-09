namespace hrms_api.DTOs;

public class DirectorRoundCandidateInsightResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DirectorRoundCandidateInsightDataDto? Data { get; set; }
}

public class DirectorRoundCandidateInsightDataDto
{
    public DirectorCandidateBasicDetailsDto Candidate { get; set; } = new();
    public DirectorCandidateDatesDto CandidateDates { get; set; } = new();
    public List<DirectorCandidateTimelineItemDto> CandidateTimeline { get; set; } = new();
    public DirectorScreeningInsightDto ScreeningInsight { get; set; } = new();
    public DirectorDiscInsightDto DiscInsight { get; set; } = new();
    public DirectorHrRoundInsightDto HrRoundInsight { get; set; } = new();
    public DirectorTechnicalAssessmentInsightDto TechnicalAssessmentInsight { get; set; } = new();
    public DirectorScoreBreakdownDto ScoreBreakdown { get; set; } = new();
    public DirectorOverallAnalysisDto OverallAnalysis { get; set; } = new();
    public List<DirectorValidationQuestionDto> DirectorQuestions { get; set; } = new();
}

public class DirectorCandidateBasicDetailsDto
{
    public int CandidateId { get; set; }
    public string? CandidateName { get; set; }
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public string? Level { get; set; }
    public int? Experience { get; set; }
    public decimal? CurrentCTC { get; set; }
    public decimal? ExpectedCTC { get; set; }
    public string? NoticePeriod { get; set; }
    public string? Source { get; set; }
    public string? CurrentStatus { get; set; }
    public string? CurrentStage { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? KeySkills { get; set; }
    public string? Remarks { get; set; }
}

public class DirectorCandidateDatesDto
{
    public string? DateOfBirth { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ShortlistingDate { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public DateTime? LatestScreeningCreatedAt { get; set; }
    public DateTime? LatestScreeningUpdatedAt { get; set; }
    public DateTime? DirectorRoundActionDate { get; set; }
    public DateTime? DirectorRoundCreatedAt { get; set; }
}

public class DirectorCandidateTimelineItemDto
{
    public int ActivityId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? TotalScore { get; set; }
    public string? Remarks { get; set; }
    public DateTime ActionDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DirectorScreeningInsightDto
{
    public decimal? ScreeningScore { get; set; }
    public string? ScreeningDecision { get; set; }
    public string? ResumeMatchSummary { get; set; }
    public string? JDMatchSummary { get; set; }
    public List<string> ScreeningStrengths { get; set; } = new();
    public List<string> ScreeningWeaknesses { get; set; } = new();
    public string? ScreeningRemarks { get; set; }
    public bool IsMissing { get; set; }
    public string? MissingReason { get; set; }
}

public class DirectorDiscInsightDto
{
    public string? DiscProfile { get; set; }
    public decimal? DScore { get; set; }
    public decimal? IScore { get; set; }
    public decimal? SScore { get; set; }
    public decimal? CScore { get; set; }
    public string? PersonalitySummary { get; set; }
    public string? WorkStyle { get; set; }
    public string? CommunicationStyle { get; set; }
    public string? StrengthBehavior { get; set; }
    public string? RiskBehavior { get; set; }
    public string? HowDirectorShouldHandleCandidate { get; set; }
    public bool IsMissing { get; set; }
    public string? MissingReason { get; set; }
}

public class DirectorHrRoundInsightDto
{
    public int? HrRoundScore { get; set; }
    public object? QuestionWiseHrPerformance { get; set; }
    public List<string> HRStrengths { get; set; } = new();
    public List<string> HRConcerns { get; set; } = new();
    public string? HRRemarks { get; set; }
    public decimal? CommunicationScore { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public decimal? CultureFitScore { get; set; }
    public decimal? AttitudeScore { get; set; }
    public decimal? LearningAbilityScore { get; set; }
    public bool IsMissing { get; set; }
    public string? MissingReason { get; set; }
}

public class DirectorTechnicalAssessmentInsightDto
{
    public int? TechnicalScore { get; set; }
    public int? AssessmentScore { get; set; }
    public decimal? AssessmentObtainedMarks { get; set; }
    public decimal? AssessmentTotalMarks { get; set; }
    public decimal? AssessmentPercentage { get; set; }
    public string? AssessmentResult { get; set; }
    public DateTime? AssessmentCreatedAt { get; set; }
    public object? QuestionWiseTechnicalPerformance { get; set; }
    public decimal? PracticalScore { get; set; }
    public object? SkillWiseScores { get; set; }
    public List<string> StrongSkillAreas { get; set; } = new();
    public List<string> WeakSkillAreas { get; set; } = new();
    public string? InterviewerRemarks { get; set; }
    public bool IsMissing { get; set; }
    public string? MissingReason { get; set; }
}

public class DirectorScoreBreakdownDto
{
    public decimal? OverallFitScore { get; set; }
    public string ScoreOutOf { get; set; } = "100";
    public string CalculationMethod { get; set; } = "Average of available score components.";
    public string Calculation { get; set; } = string.Empty;
    public List<DirectorScoreComponentDto> Components { get; set; } = new();
}

public class DirectorScoreComponentDto
{
    public string Name { get; set; } = string.Empty;
    public decimal? Score { get; set; }
    public decimal OutOf { get; set; } = 100;
    public bool IncludedInOverall { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? Explanation { get; set; }
}

public class DirectorOverallAnalysisDto
{
    public decimal? OverallFitScore { get; set; }
    public string RiskLevel { get; set; } = "Medium";
    public string FinalRecommendation { get; set; } = "Hold";
    public List<string> CandidateStrengthSummary { get; set; } = new();
    public List<string> CandidateWeaknessSummary { get; set; } = new();
    public string? HiringRiskReason { get; set; }
    public string? RoleFitSummary { get; set; }
    public string? CultureFitSummary { get; set; }
    public string? LearningPotential { get; set; }
    public string? StabilityPrediction { get; set; }
    public string? SalaryRisk { get; set; }
    public string? NoticePeriodRisk { get; set; }
}

public class DirectorValidationQuestionDto
{
    public int QuestionNo { get; set; }
    public string Question { get; set; } = string.Empty;
    public string? WhyDirectorShouldAskThis { get; set; }
    public string? WhatStrongAnswerLooksLike { get; set; }
    public string? RedFlagAnswer { get; set; }
    public string? RelatedRiskOrSkill { get; set; }
}
