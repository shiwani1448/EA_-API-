using hrms_api.DTOs;
using hrms_api.Services;
using Microsoft.AspNetCore.Mvc;

namespace hrms_api.Controllers;

[ApiController]
[Route("")]
[Produces("application/json")]
public class HrAiInterviewQuestionsController : ControllerBase
{
    private readonly IHrAiInterviewQuestionService _service;

    public HrAiInterviewQuestionsController(IHrAiInterviewQuestionService service)
    {
        _service = service;
    }

    [HttpPost("generate-hr-ai-interview-questions")]
    [ProducesResponseType(typeof(HrAiInterviewQuestionsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HrAiInterviewQuestionsResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HrAiInterviewQuestionsResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HrAiInterviewQuestionsResponseDto), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<HrAiInterviewQuestionsResponseDto>> Generate(
        [FromBody] GenerateHrAiInterviewQuestionsRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _service.GenerateAsync(request, cancellationToken);
        return StatusCode(result.StatusCode, result.Response);
    }

    [HttpGet("hr-ai-interview-questions/{candidateId:int}/{interviewRoundId:int}")]
    [ProducesResponseType(typeof(HrAiInterviewQuestionsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HrAiInterviewQuestionsResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HrAiInterviewQuestionsResponseDto>> Get(
        int candidateId,
        int interviewRoundId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAsync(candidateId, interviewRoundId, cancellationToken);
        return StatusCode(result.StatusCode, result.Response);
    }
}
