using hrms_api.DTOs;
using hrms_api.Services;
using Microsoft.AspNetCore.Mvc;

namespace hrms_api.Controllers;

[ApiController]
[Route("api/director-ai-interview-questions")]
[Produces("application/json")]
public class DirectorAiInterviewQuestionsController : ControllerBase
{
    private readonly IDirectorAiInterviewQuestionService _service;

    public DirectorAiInterviewQuestionsController(IDirectorAiInterviewQuestionService service)
    {
        _service = service;
    }

    [HttpPost("generate")]
    [ProducesResponseType(typeof(DirectorAiInterviewQuestionsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DirectorAiInterviewQuestionsResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(DirectorAiInterviewQuestionsResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(DirectorAiInterviewQuestionsResponseDto), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<DirectorAiInterviewQuestionsResponseDto>> Generate(
        [FromBody] GenerateDirectorAiInterviewQuestionsRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _service.GenerateAsync(request, cancellationToken);
        return StatusCode(result.StatusCode, result.Response);
    }

    [HttpGet("{candidateId:int}/{interviewRoundId:int}")]
    [ProducesResponseType(typeof(DirectorAiInterviewQuestionsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DirectorAiInterviewQuestionsResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DirectorAiInterviewQuestionsResponseDto>> Get(
        int candidateId,
        int interviewRoundId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAsync(candidateId, interviewRoundId, cancellationToken);
        return StatusCode(result.StatusCode, result.Response);
    }
}
