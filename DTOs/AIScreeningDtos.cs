namespace hrms_api.DTOs;

public class AiScreeningBatchRequestDto
{
    public List<int>? CandidateIds { get; set; }
    public bool ForceRescreen { get; set; }
}

public class AiScreeningBatchResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Completed { get; set; }
    public int Shortlisted { get; set; }
    public int Rejected { get; set; }
    public int PendingReview { get; set; }
    public int Failed { get; set; }
    public List<AiScreeningResultItemDto> Results { get; set; } = new();
}

public class ScreeningStartResponseDto
{
    public string BatchId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalCandidates { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ScreeningBatchStatusDto
{
    public string BatchId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalCandidates { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public int PendingReview { get; set; }
    public int Shortlisted { get; set; }
    public int Rejected { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ScreeningBatchResultDto : ScreeningBatchStatusDto
{
    public List<ScreeningCandidateResultDto> Results { get; set; } = new();
}

public class ScreeningCandidateResultDto
{
    public int CandidateId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public bool JDTextAvailable { get; set; }
    public bool ResumeTextAvailable { get; set; }
    public string? JDExtractionMethod { get; set; }
    public string? ResumeExtractionMethod { get; set; }
    public string? JDExtractionStatus { get; set; }
    public string? ResumeExtractionStatus { get; set; }
    public string? ScreeningStatus { get; set; }
    public string? AIStatus { get; set; }
    public decimal? Score { get; set; }
    public string? Decision { get; set; }
    public string? Recommendation { get; set; }
    public string? FailureReason { get; set; }
    public int NormalTextLength { get; set; }
    public int PdfPageCount { get; set; }
    public bool RenderedImageCreated { get; set; }
    public string? RenderedImagePath { get; set; }
    public int OCRTextLength { get; set; }
    public string? ExtractionMethodUsed { get; set; }
    public string? CurrentStep { get; set; }
    public string? FailureStep { get; set; }
    public string? ExceptionType { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? InnerExceptionMessage { get; set; }
    public int AiPromptLength { get; set; }
    public int AiResponseLength { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ScreeningDebugCandidateDto
{
    public int CandidateId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string? CurrentStep { get; set; }
    public string? FailureStep { get; set; }
    public string? ExceptionType { get; set; }
    public string? ExceptionMessage { get; set; }
    public int NormalTextLength { get; set; }
    public int OcrTextLength { get; set; }
    public int AiPromptLength { get; set; }
    public int AiResponseLength { get; set; }
    public int DurationSeconds { get; set; }
}

public class DocumentExtractionTestResponseDto
{
    public bool IsSelectableTextPdf { get; set; }
    public int NormalExtractedTextLength { get; set; }
    public bool IsImageBasedPdf { get; set; }
    public int PageCount { get; set; }
    public int OCRExtractedTextLength { get; set; }
    public string First1000Characters { get; set; } = string.Empty;
    public string ExtractionMethod { get; set; } = string.Empty;
    public string? FailureStep { get; set; }
    public string? FailureReason { get; set; }
}

public class DocumentExtractionHealthDto
{
    public bool PopplerAvailable { get; set; }
    public string PdfToPpmPath { get; set; } = string.Empty;
    public bool TesseractAvailable { get; set; }
    public string TesseractDataPath { get; set; } = string.Empty;
    public bool TempFolderWritable { get; set; }
}

public class AiScreeningResultItemDto
{
    public int CandidateId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public decimal? Score { get; set; }
    public string? Decision { get; set; }
    public string? Recommendation { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class AiScreeningReportDto
{
    public int Id { get; set; }
    public string? BatchId { get; set; }
    public int CandidateId { get; set; }
    public string? CandidateName { get; set; }
    public int? RequisitionId { get; set; }
    public string? ResumePath { get; set; }
    public string? JDPath { get; set; }
    public bool JDTextAvailable { get; set; }
    public bool ResumeTextAvailable { get; set; }
    public string? JDExtractionMethod { get; set; }
    public string? ResumeExtractionMethod { get; set; }
    public string? JDExtractionStatus { get; set; }
    public string? ResumeExtractionStatus { get; set; }
    public string? ScreeningStatus { get; set; }
    public string? AIStatus { get; set; }
    public string? FailureReason { get; set; }
    public int NormalTextLength { get; set; }
    public int PdfPageCount { get; set; }
    public bool RenderedImageCreated { get; set; }
    public string? RenderedImagePath { get; set; }
    public int OCRTextLength { get; set; }
    public string? ExtractionMethodUsed { get; set; }
    public string? CurrentStep { get; set; }
    public string? FailureStep { get; set; }
    public string? ExceptionType { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? InnerExceptionMessage { get; set; }
    public int AiPromptLength { get; set; }
    public int AiResponseLength { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime? StartedAt { get; set; }
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
    public List<string>? MatchedSkills { get; set; }
    public List<string>? MissingSkills { get; set; }
    public List<string>? Strengths { get; set; }
    public List<string>? Concerns { get; set; }
    public List<string>? RedFlags { get; set; }
    public List<string>? InterviewFocusAreas { get; set; }
    public string? ShortSummary { get; set; }
    public string? ErrorMessage { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AiScreeningScoreDetailsDto
{
    public int CandidateId { get; set; }
    public int? RequisitionId { get; set; }
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
    public List<string>? MatchedSkills { get; set; }
    public List<string>? MissingSkills { get; set; }
    public List<string>? Strengths { get; set; }
    public List<string>? Concerns { get; set; }
    public List<string>? RedFlags { get; set; }
    public List<string>? InterviewFocusAreas { get; set; }
    public string? ShortSummary { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
