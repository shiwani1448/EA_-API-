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

    // ============================================================
    // TASK REVIEW / REWORK (Phase 1) — entirely separate from submit/approve/reject/
    // request-changes/resubmit above (the existing WorkflowStatus/ApprovalCycle gate).
    // ============================================================

    /// <summary>Submit the Approval Request's central task for review. 409 if Cancelled, already Completed, or a review is already pending.</summary>
    [HttpPost("submit-for-review")]
    [ProducesResponseType(typeof(TaskReviewSummaryDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> SubmitForReview(long approvalRequestId, [FromBody] SubmitForReviewRequestDto? dto, CancellationToken ct)
        => Ok(await _lifecycle.SubmitForReviewAsync(approvalRequestId, dto ?? new SubmitForReviewRequestDto(), ct));

    /// <summary>Approve the current pending review cycle. 409 if no review is currently pending.</summary>
    [HttpPost("review/approve")]
    [ProducesResponseType(typeof(TaskReviewSummaryDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> ApproveReview(long approvalRequestId, [FromBody] ApproveTaskReviewRequestDto? dto, CancellationToken ct)
        => Ok(await _lifecycle.ApproveReviewAsync(approvalRequestId, dto ?? new ApproveTaskReviewRequestDto(), ct));

    /// <summary>Send the current pending review cycle back for rework. 409 if no review is currently pending.</summary>
    [HttpPost("review/rework")]
    [ProducesResponseType(typeof(TaskReviewSummaryDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> RequestRework(long approvalRequestId, [FromBody] RequestTaskReworkRequestDto? dto, CancellationToken ct)
        => Ok(await _lifecycle.RequestTaskReworkAsync(approvalRequestId, dto ?? new RequestTaskReworkRequestDto(), ct));

    /// <summary>Full review-cycle history, oldest (cycle 1) first. EaTask-based — no WorkflowInstanceId is exposed.</summary>
    [HttpGet("review/history")]
    [ProducesResponseType(typeof(List<TaskReviewHistoryItemDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> ReviewHistory(long approvalRequestId, CancellationToken ct)
        => Ok(await _lifecycle.GetReviewHistoryAsync(approvalRequestId, ct));
}
