using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/approvals/{approvalRequestId:long}")]
public class ApprovalsLifecycleController : ControllerBase
{
    private readonly IApprovalLifecycleService _lifecycle;

    public ApprovalsLifecycleController(IApprovalLifecycleService lifecycle) => _lifecycle = lifecycle;

    [HttpPost("submit")]
    public async Task<IActionResult> Submit(long approvalRequestId, CancellationToken ct)
        => Ok(await _lifecycle.SubmitAsync(approvalRequestId, ct));

    [HttpPost("approve")]
    public async Task<IActionResult> Approve(long approvalRequestId, [FromBody] ApprovalDecisionDto dto, CancellationToken ct)
        => Ok(await _lifecycle.ApproveAsync(approvalRequestId, dto, ct));

    [HttpPost("reject")]
    public async Task<IActionResult> Reject(long approvalRequestId, [FromBody] ApprovalDecisionDto dto, CancellationToken ct)
        => Ok(await _lifecycle.RejectAsync(approvalRequestId, dto, ct));

    [HttpPost("request-changes")]
    public async Task<IActionResult> RequestChanges(long approvalRequestId, [FromBody] ApprovalDecisionDto dto, CancellationToken ct)
        => Ok(await _lifecycle.RequestChangesAsync(approvalRequestId, dto, ct));

    [HttpPost("resubmit")]
    public async Task<IActionResult> Resubmit(long approvalRequestId, CancellationToken ct)
        => Ok(await _lifecycle.ResubmitAsync(approvalRequestId, ct));

}
