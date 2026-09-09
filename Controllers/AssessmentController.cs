using System.Text.Json;
using hrms_api.DTOs;
using hrms_api.Services;
using Microsoft.AspNetCore.Mvc;

namespace hrms_api.Controllers;

[ApiController]
[Route("api/assessment")]
[Produces("application/json")]
public class AssessmentController : ControllerBase
{
    private readonly IAssessmentEvaluationService _assessmentEvaluationService;

    public AssessmentController(IAssessmentEvaluationService assessmentEvaluationService)
    {
        _assessmentEvaluationService = assessmentEvaluationService;
    }

    [HttpPost("evaluate-and-save")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(100_000_000)]
    [ProducesResponseType(typeof(ApiResponse<AssessmentEvaluationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AssessmentEvaluationResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AssessmentEvaluationResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AssessmentEvaluationResponseDto>>> EvaluateAndSave(
        [FromForm] AssessmentEvaluateAndSaveRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _assessmentEvaluationService.EvaluateAndSaveAsync(
                request,
                Request.Form.Files,
                cancellationToken);

            return Ok(ApiResponse<AssessmentEvaluationResponseDto>.Ok(
                result,
                "Assessment evaluation saved successfully."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<AssessmentEvaluationResponseDto>.Fail(ex.Message));
        }
        catch (JsonException ex)
        {
            return BadRequest(ApiResponse<AssessmentEvaluationResponseDto>.Fail($"Invalid questions JSON: {ex.Message}"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<AssessmentEvaluationResponseDto>.Fail(ex.Message));
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<AssessmentEvaluationWithCandidateResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<AssessmentEvaluationWithCandidateResponseDto>>>> GetAll(
        [FromQuery] int? candidateId,
        CancellationToken cancellationToken)
    {
        var result = await _assessmentEvaluationService.GetAllAsync(candidateId, cancellationToken);

        return Ok(ApiResponse<List<AssessmentEvaluationWithCandidateResponseDto>>.Ok(
            result,
            "Assessment evaluations fetched successfully."));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<AssessmentEvaluationWithCandidateResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AssessmentEvaluationWithCandidateResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AssessmentEvaluationWithCandidateResponseDto>>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _assessmentEvaluationService.GetByIdAsync(id, cancellationToken);

        if (result is null)
            return NotFound(ApiResponse<AssessmentEvaluationWithCandidateResponseDto>.Fail("Assessment evaluation not found."));

        return Ok(ApiResponse<AssessmentEvaluationWithCandidateResponseDto>.Ok(
            result,
            "Assessment evaluation fetched successfully."));
    }

    [HttpGet("candidate/{candidateId:int}")]
    [ProducesResponseType(typeof(ApiResponse<List<AssessmentEvaluationWithCandidateResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<AssessmentEvaluationWithCandidateResponseDto>>>> GetByCandidateId(
        int candidateId,
        CancellationToken cancellationToken)
    {
        var result = await _assessmentEvaluationService.GetAllAsync(candidateId, cancellationToken);

        return Ok(ApiResponse<List<AssessmentEvaluationWithCandidateResponseDto>>.Ok(
            result,
            "Assessment evaluations fetched successfully."));
    }

    [HttpGet("candidate/{candidateId:int}/latest")]
    [ProducesResponseType(typeof(ApiResponse<AssessmentEvaluationWithCandidateResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AssessmentEvaluationWithCandidateResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AssessmentEvaluationWithCandidateResponseDto>>> GetLatestByCandidateId(
        int candidateId,
        CancellationToken cancellationToken)
    {
        var result = await _assessmentEvaluationService.GetLatestByCandidateIdAsync(candidateId, cancellationToken);

        if (result is null)
            return NotFound(ApiResponse<AssessmentEvaluationWithCandidateResponseDto>.Fail("No assessment evaluation found for this candidate."));

        return Ok(ApiResponse<AssessmentEvaluationWithCandidateResponseDto>.Ok(
            result,
            "Latest assessment evaluation fetched successfully."));
    }
}
