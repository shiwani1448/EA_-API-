namespace hrms_api.DTOs;

public class GenerateDirectorAiInterviewQuestionsRequestDto
{
    public int CandidateId { get; set; }
    public int InterviewRoundId { get; set; }
}

public class DirectorAiInterviewQuestionItemDto
{
    public int Id { get; set; }
    public int QuestionNo { get; set; }
    public string Question { get; set; } = string.Empty;
    public string? WhyDirectorShouldAskThis { get; set; }
    public string? GapOrRiskArea { get; set; }
    public List<string> StrongAnswerSignals { get; set; } = new();
    public List<string> RedFlagSignals { get; set; } = new();
}

public class DirectorAiInterviewQuestionsResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int CandidateId { get; set; }
    public int InterviewRoundId { get; set; }
    public DirectorCandidateBasicDetailsDto? Candidate { get; set; }
    public DirectorCandidateDatesDto? CandidateDates { get; set; }
    public List<DirectorCandidateTimelineItemDto> CandidateTimeline { get; set; } = new();
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public string? Level { get; set; }
    public decimal? ScreeningScore { get; set; }
    public string? DiscProfile { get; set; }
    public decimal? DScore { get; set; }
    public decimal? IScore { get; set; }
    public decimal? SScore { get; set; }
    public decimal? CScore { get; set; }
    public decimal? HrRoundScore { get; set; }
    public decimal? TechnicalAssessmentScore { get; set; }
    public DirectorScoreBreakdownDto? ScoreBreakdown { get; set; }
    public DirectorOverallAnalysisDto? OverallAnalysis { get; set; }
    public string? DirectorSummary { get; set; }
    public string? OverallGapSummary { get; set; }
    public List<DirectorAiInterviewQuestionItemDto> Questions { get; set; } = new();
}
