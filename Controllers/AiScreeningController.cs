using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace hrms_api.Controllers;

[ApiController]
[Route("api/ai-screening")]
[Produces("application/json")]
public class AiScreeningController : ControllerBase
{
    private readonly IAiScreeningService _screeningService;
    private readonly AppDbContext _db;

    public AiScreeningController(IAiScreeningService screeningService, AppDbContext db)
    {
        _screeningService = screeningService;
        _db               = db;
    }

    /// <summary>
    /// Run AI screening for a batch of candidates.
    /// Leave candidateIds null/empty to auto-select all eligible candidates.
    /// </summary>
    [HttpPost("run-batch")]
    [ProducesResponseType(typeof(AiScreeningBatchResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AiScreeningBatchResponseDto>> RunBatch(
        [FromBody] AiScreeningBatchRequestDto request)
    {
        var result = await _screeningService.RunBatchAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// Get the latest AI screening report for a candidate.
    /// </summary>
    [HttpGet("report/{candidateId:int}")]
    [ProducesResponseType(typeof(ApiResponse<AiScreeningReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AiScreeningReportDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AiScreeningReportDto>>> GetReport(int candidateId)
    {
        var screening = await _db.CandidateAIScreenings
            .Where(s => s.CandidateId == candidateId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (screening is null)
            return NotFound(ApiResponse<AiScreeningReportDto>.Fail(
                $"No AI screening report found for candidate {candidateId}."));

        return Ok(ApiResponse<AiScreeningReportDto>.Ok(MapToDto(screening), "Report fetched successfully."));
    }

    /// <summary>
    /// Get latest AI score details for a candidate.
    /// </summary>
    [HttpGet("score-details/{candidateId:int}")]
    [ProducesResponseType(typeof(ApiResponse<AiScreeningScoreDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AiScreeningScoreDetailsDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AiScreeningScoreDetailsDto>>> GetScoreDetails(int candidateId)
    {
        var screening = await _db.CandidateAIScreenings
            .Where(s => s.CandidateId == candidateId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (screening is null)
            return NotFound(ApiResponse<AiScreeningScoreDetailsDto>.Fail(
                $"No AI screening score found for candidate {candidateId}."));

        return Ok(ApiResponse<AiScreeningScoreDetailsDto>.Ok(
            MapToScoreDetailsDto(screening),
            "AI score details fetched successfully."));
    }

    /// <summary>
    /// Get all AI screening reports for a candidate (history).
    /// </summary>
    [HttpGet("reports/{candidateId:int}")]
    [ProducesResponseType(typeof(ApiResponse<List<AiScreeningReportDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<AiScreeningReportDto>>>> GetAllReports(int candidateId)
    {
        var screenings = await _db.CandidateAIScreenings
            .Where(s => s.CandidateId == candidateId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<AiScreeningReportDto>>.Ok(
            screenings.Select(MapToDto).ToList(),
            "Reports fetched successfully."));
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static AiScreeningReportDto MapToDto(hrms_api.Models.CandidateAIScreening s) => new()
    {
        Id                        = s.Id,
        BatchId                   = s.BatchId,
        CandidateId               = s.CandidateId,
        CandidateName             = s.CandidateName,
        RequisitionId             = s.RequisitionId,
        ResumePath                = s.ResumePath,
        JDPath                    = s.JDPath,
        JDTextAvailable           = s.JDTextAvailable,
        ResumeTextAvailable       = s.ResumeTextAvailable,
        JDExtractionMethod        = s.JDExtractionMethod,
        ResumeExtractionMethod    = s.ResumeExtractionMethod,
        JDExtractionStatus        = s.JDExtractionStatus,
        ResumeExtractionStatus    = s.ResumeExtractionStatus,
        ScreeningStatus           = s.ScreeningStatus,
        AIStatus                  = s.AIStatus,
        FailureReason             = s.FailureReason,
        NormalTextLength          = s.NormalTextLength,
        PdfPageCount              = s.PdfPageCount,
        RenderedImageCreated      = s.RenderedImageCreated,
        RenderedImagePath         = s.RenderedImagePath,
        OCRTextLength             = s.OCRTextLength,
        ExtractionMethodUsed      = s.ExtractionMethodUsed,
        FailureStep               = s.FailureStep,
        StartedAt                 = s.StartedAt,
        CompletedAt               = s.CompletedAt,
        OverallScore              = s.OverallScore,
        Recommendation            = s.Recommendation,
        Decision                  = s.Decision,
        ConfidenceScore           = s.ConfidenceScore,
        RiskLevel                 = s.RiskLevel,
        RoleFitScore              = s.RoleFitScore,
        SkillFitScore             = s.SkillFitScore,
        ExperienceRelevanceScore  = s.ExperienceRelevanceScore,
        AchievementImpactScore    = s.AchievementImpactScore,
        CareerStabilityScore      = s.CareerStabilityScore,
        EducationCertificationScore = s.EducationCertificationScore,
        IndustryAlignmentScore    = s.IndustryAlignmentScore,
        GrowthPotentialScore      = s.GrowthPotentialScore,
        RedFlagDeduction          = s.RedFlagDeduction,
        ShortSummary              = s.ShortSummary,
        ErrorMessage              = s.ErrorMessage,
        Status                    = s.Status,
        CreatedAt                 = s.CreatedAt,
        UpdatedAt                 = s.UpdatedAt,
        MatchedSkills             = DeserializeList(s.MatchedSkillsJson),
        MissingSkills             = DeserializeList(s.MissingSkillsJson),
        Strengths                 = DeserializeList(s.StrengthsJson),
        Concerns                  = DeserializeList(s.ConcernsJson),
        RedFlags                  = DeserializeList(s.RedFlagsJson),
        InterviewFocusAreas       = DeserializeList(s.InterviewFocusAreasJson)
    };

    private static AiScreeningScoreDetailsDto MapToScoreDetailsDto(hrms_api.Models.CandidateAIScreening s) => new()
    {
        CandidateId                 = s.CandidateId,
        RequisitionId               = s.RequisitionId,
        OverallScore                = s.OverallScore,
        Recommendation              = s.Recommendation,
        Decision                    = s.Decision,
        ConfidenceScore             = s.ConfidenceScore,
        RiskLevel                   = s.RiskLevel,
        RoleFitScore                = s.RoleFitScore,
        SkillFitScore               = s.SkillFitScore,
        ExperienceRelevanceScore    = s.ExperienceRelevanceScore,
        AchievementImpactScore      = s.AchievementImpactScore,
        CareerStabilityScore        = s.CareerStabilityScore,
        EducationCertificationScore = s.EducationCertificationScore,
        IndustryAlignmentScore      = s.IndustryAlignmentScore,
        GrowthPotentialScore        = s.GrowthPotentialScore,
        RedFlagDeduction            = s.RedFlagDeduction,
        MatchedSkills               = DeserializeList(s.MatchedSkillsJson),
        MissingSkills               = DeserializeList(s.MissingSkillsJson),
        Strengths                   = DeserializeList(s.StrengthsJson),
        Concerns                    = DeserializeList(s.ConcernsJson),
        RedFlags                    = DeserializeList(s.RedFlagsJson),
        InterviewFocusAreas         = DeserializeList(s.InterviewFocusAreasJson),
        ShortSummary                = s.ShortSummary,
        Status                      = s.Status,
        ErrorMessage                = s.ErrorMessage,
        CreatedAt                   = s.CreatedAt,
        UpdatedAt                   = s.UpdatedAt
    };

    private static List<string>? DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<List<string>>(json); }
        catch { return null; }
    }
}
