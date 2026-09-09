using Jarvis5.Dtos.Approval;
using Jarvis5.Services;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/request/{requestId:long}")]
public class ApprovalController : ControllerBase
{
    private readonly IApprovalService _approvalService;

    public ApprovalController(IApprovalService approvalService)
    {
        _approvalService = approvalService;
    }

    /// <summary>Submits the completed Analysis + Solution Design for Director
    /// review (Stage 4). Requires the Solution Design to already be approved.</summary>
    [HttpPost("submit-for-approval")]
    [ProducesResponseType(typeof(ApprovalDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApprovalDetailDto>> SubmitForApproval(long requestId, CancellationToken ct)
    {
        var result = await _approvalService.SubmitForApprovalAsync(requestId, ct);
        return Ok(result);
    }

    /// <summary>Approves the current pending round and moves the request to
    /// Stage 5 (Development).</summary>
    [HttpPost("approve")]
    [ProducesResponseType(typeof(ApprovalDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApprovalDetailDto>> Approve(long requestId, [FromBody] ApproveRequestDto dto, CancellationToken ct)
    {
        var result = await _approvalService.ApproveAsync(requestId, dto, ct);
        return Ok(result);
    }

    /// <summary>Rejects the current pending round and sends the request back to
    /// Stage 2 (Analysis) for rework. Rejection Reason, Improvement Areas and
    /// Comments are all mandatory.</summary>
    [HttpPost("reject")]
    [ProducesResponseType(typeof(ApprovalDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApprovalDetailDto>> Reject(long requestId, [FromBody] RejectRequestDto dto, CancellationToken ct)
    {
        var result = await _approvalService.RejectAsync(requestId, dto, ct);
        return Ok(result);
    }

    /// <summary>Aggregated data for the Approval screen: request info, latest
    /// analysis/solution-design summaries, and the current + previous approval round.</summary>
    [HttpGet("approval")]
    [ProducesResponseType(typeof(ApprovalDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApprovalDetailDto>> GetApproval(long requestId, CancellationToken ct)
    {
        var result = await _approvalService.GetDetailAsync(requestId, ct);
        return Ok(result);
    }

    /// <summary>Full approval round history for a request — "View Rework History".</summary>
    [HttpGet("approval/rounds")]
    [ProducesResponseType(typeof(List<ApprovalRoundDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ApprovalRoundDto>>> GetApprovalRounds(long requestId, CancellationToken ct)
    {
        var result = await _approvalService.GetRoundsAsync(requestId, ct);
        return Ok(result);
    }
}
