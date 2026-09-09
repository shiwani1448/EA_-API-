namespace hrms_api.DTOs;

public class GenerateHrAiInterviewQuestionsRequestDto
{
    public int CandidateId { get; set; }
    public int InterviewRoundId { get; set; }
}

public class HrAiInterviewQuestionItemDto
{
    public int Id { get; set; }
    public int QuestionNo { get; set; }
    public string Question { get; set; } = string.Empty;
    public string? WhyAskThis { get; set; }
    public string? ScoringGuideline { get; set; }
    public List<string> StrongAnswerSignals { get; set; } = new();
    public List<string> RedFlagSignals { get; set; } = new();
}

public class HrAiInterviewQuestionsResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int CandidateId { get; set; }
    public int InterviewRoundId { get; set; }
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public string? DiscProfile { get; set; }
    public List<HrAiInterviewQuestionItemDto> Questions { get; set; } = new();
}
