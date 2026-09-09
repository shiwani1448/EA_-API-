using hrms_api.DTOs;
using hrms_api.Services;
using Microsoft.AspNetCore.Mvc;

namespace hrms_api.Controllers;

[ApiController]
[Route("api/director-round")]
[Produces("application/json")]
public class DirectorRoundController : ControllerBase
{
    private readonly IDirectorRoundInsightService _insightService;

    public DirectorRoundController(IDirectorRoundInsightService insightService)
    {
        _insightService = insightService;
    }

    [HttpGet("candidate-insight/{candidateId:int}/{interviewRoundId:int}")]
    [ProducesResponseType(typeof(DirectorRoundCandidateInsightResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DirectorRoundCandidateInsightResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(DirectorRoundCandidateInsightResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DirectorRoundCandidateInsightResponseDto>> GetCandidateInsight(
        int candidateId,
        int interviewRoundId,
        CancellationToken cancellationToken)
    {
        var result = await _insightService.GetCandidateInsightAsync(candidateId, interviewRoundId, cancellationToken);
        return StatusCode(result.StatusCode, result.Response);
    }
}
