namespace hrms_api.Models;

public class HrAiInterviewQuestion
{
    public int Id { get; set; }
    public int CandidateId { get; set; }
    public int InterviewRoundId { get; set; }
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public string? DiscProfile { get; set; }
    public decimal? DScore { get; set; }
    public decimal? IScore { get; set; }
    public decimal? SScore { get; set; }
    public decimal? CScore { get; set; }
    public int QuestionNo { get; set; }
    public string Question { get; set; } = string.Empty;
    public string? WhyAskThis { get; set; }
    public string? ScoringGuideline { get; set; }
    public string? StrongAnswerSignals { get; set; }
    public string? RedFlagSignals { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
