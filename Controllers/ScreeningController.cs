using hrms_api.DTOs;
using hrms_api.Services;
using Microsoft.AspNetCore.Mvc;

namespace hrms_api.Controllers;

[ApiController]
[Route("screening")]
[Produces("application/json")]
public class ScreeningController : ControllerBase
{
    private readonly IScreeningBatchService _screeningBatchService;

    public ScreeningController(IScreeningBatchService screeningBatchService)
    {
        _screeningBatchService = screeningBatchService;
    }

    [HttpPost("start")]
    [ProducesResponseType(typeof(ScreeningStartResponseDto), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<ScreeningStartResponseDto>> Start(
        [FromBody] AiScreeningBatchRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _screeningBatchService.StartAsync(request, cancellationToken);
        return AcceptedAtAction(nameof(Status), new { batchId = result.BatchId }, result);
    }

    [HttpGet("status/{batchId}")]
    [ProducesResponseType(typeof(ScreeningBatchStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScreeningBatchStatusDto>> Status(
        string batchId,
        CancellationToken cancellationToken)
    {
        var status = await _screeningBatchService.GetStatusAsync(batchId, cancellationToken);
        return status is null ? NotFound() : Ok(status);
    }

    [HttpGet("result/{batchId}")]
    [ProducesResponseType(typeof(ScreeningBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScreeningBatchResultDto>> Result(
        string batchId,
        CancellationToken cancellationToken)
    {
        var result = await _screeningBatchService.GetResultAsync(batchId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("debug/{batchId}")]
    [ProducesResponseType(typeof(List<ScreeningDebugCandidateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ScreeningDebugCandidateDto>>> Debug(
        string batchId,
        CancellationToken cancellationToken)
    {
        var result = await _screeningBatchService.GetDebugAsync(batchId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
