using System.ComponentModel.DataAnnotations.Schema;

namespace hrms_api.Models;

public class CandidateAIScreening
{
    public int Id { get; set; }
    [NotMapped]
    public string? BatchId { get; set; }
    public int CandidateId { get; set; }
    [NotMapped]
    public string? CandidateName { get; set; }
    public int? RequisitionId { get; set; }
    public string? ResumePath { get; set; }
    public string? JDPath { get; set; }
    public string? ResumeExtractedText { get; set; }
    public string? JDExtractedText { get; set; }
    [NotMapped]
    public bool JDTextAvailable { get; set; }
    [NotMapped]
    public bool ResumeTextAvailable { get; set; }
    [NotMapped]
    public string? JDExtractionMethod { get; set; }
    [NotMapped]
    public string? ResumeExtractionMethod { get; set; }
    [NotMapped]
    public string? JDExtractionStatus { get; set; }
    [NotMapped]
    public string? ResumeExtractionStatus { get; set; }
    [NotMapped]
    public string? ScreeningStatus { get; set; } = "Pending Review";
    [NotMapped]
    public string? AIStatus { get; set; } = "Skipped";
    [NotMapped]
    public string? FailureReason { get; set; }
    [NotMapped]
    public int NormalTextLength { get; set; }
    [NotMapped]
    public int PdfPageCount { get; set; }
    [NotMapped]
    public bool RenderedImageCreated { get; set; }
    [NotMapped]
    public string? RenderedImagePath { get; set; }
    [NotMapped]
    public int OCRTextLength { get; set; }
    [NotMapped]
    public string? ExtractionMethodUsed { get; set; }
    [NotMapped]
    public string? CurrentStep { get; set; }
    [NotMapped]
    public string? FailureStep { get; set; }
    [NotMapped]
    public string? ExceptionType { get; set; }
    [NotMapped]
    public string? ExceptionMessage { get; set; }
    [NotMapped]
    public string? InnerExceptionMessage { get; set; }
    [NotMapped]
    public int AiPromptLength { get; set; }
    [NotMapped]
    public int AiResponseLength { get; set; }
    [NotMapped]
    public int DurationSeconds { get; set; }
    [NotMapped]
    public DateTime? StartedAt { get; set; }
    [NotMapped]
    public DateTime? CompletedAt { get; set; }
    public decimal? OverallScore { get; set; }
    public string? Recommendation { get; set; }
    public string? Decision { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public string? RiskLevel { get; set; }
    public decimal? RoleFitScore { get; set; }
    public decimal? SkillFitScore { get; set; }
    public decimal? ExperienceRelevanceScore { get; set; }
    public decimal? AchievementImpactScore { get; set; }
    public decimal? CareerStabilityScore { get; set; }
    public decimal? EducationCertificationScore { get; set; }
    public decimal? IndustryAlignmentScore { get; set; }
    public decimal? GrowthPotentialScore { get; set; }
    public decimal? RedFlagDeduction { get; set; }
    public string? MatchedSkillsJson { get; set; }
    public string? MissingSkillsJson { get; set; }
    public string? StrengthsJson { get; set; }
    public string? ConcernsJson { get; set; }
    public string? RedFlagsJson { get; set; }
    public string? InterviewFocusAreasJson { get; set; }
    public string? ShortSummary { get; set; }
    [Column("RawOllamaResponse")]
    public string? RawClaudeResponse { get; set; }
    public string? ErrorMessage { get; set; }
    public string Status { get; set; } = "Pending Review";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

