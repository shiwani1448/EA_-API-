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

    [HttpPost("start")]
    [ProducesResponseType(typeof(ApprovalDetailDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> StartActual(long approvalRequestId, CancellationToken ct)
        => Ok(await _lifecycle.StartActualAsync(approvalRequestId, ct));

    [HttpPost("review/start")]
    [ProducesResponseType(typeof(ApprovalDetailDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> StartReview(long approvalRequestId, CancellationToken ct)
        => Ok(await _lifecycle.StartReviewAsync(approvalRequestId, ct));

    [HttpPost("rework/start")]
    [ProducesResponseType(typeof(ApprovalDetailDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> StartRework(long approvalRequestId, CancellationToken ct)
        => Ok(await _lifecycle.StartReworkAsync(approvalRequestId, ct));

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
    // PAUSE / RESUME — shared WorkPause architecture, mirrors Delegation's.
    // ============================================================

    /// <summary>
    /// Pause an explicitly started, InProgress Approval. Optional body <c>{ "pauseReason": "..." }</c>. 404 unknown id; 409 when not
    /// InProgress or already paused. Returns the same ApprovalDetailDto shape as GET-by-id.
    /// </summary>
    [HttpPost("pause")]
    [ProducesResponseType(typeof(ApprovalDetailDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> Pause(long approvalRequestId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] ApprovalPauseRequestDto? request,
        CancellationToken ct)
        => Ok(await _lifecycle.PauseAsync(approvalRequestId, request, ct));

    /// <summary>Resume a paused Approval. No request body. 404 unknown id; 409 when not InProgress or not currently paused.</summary>
    [HttpPost("resume")]
    [ProducesResponseType(typeof(ApprovalDetailDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> Resume(long approvalRequestId, CancellationToken ct)
        => Ok(await _lifecycle.ResumeAsync(approvalRequestId, ct));

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

    /// <summary>
    /// Approve the current pending review cycle. multipart/form-data with an optional <c>attachment</c>
    /// file so the assignee can attach their own document to the approval. 409 if no review is
    /// currently pending. Never touches WorkflowStatus/ApprovalCycle/eaTask.ExecutionStatus — the
    /// business decision stays owned exclusively by POST .../approve and .../reject above.
    /// </summary>
    [HttpPost("review/approve")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(27 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 27 * 1024 * 1024)]
    [ProducesResponseType(typeof(TaskReviewSummaryDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> ApproveReview(long approvalRequestId, [FromForm] ApprovalApproveReviewRequestDto request, CancellationToken ct) =>
        Ok(await _lifecycle.ApproveReviewAsync(approvalRequestId,
            new ApproveTaskReviewRequestDto { ReviewedById = request.ReviewedById, ReviewedByName = request.ReviewedByName, ReviewRemark = request.ReviewRemark },
            request.Attachment, ct));

    /// <summary>
    /// Send the current pending review cycle back for rework. multipart/form-data with an optional
    /// <c>attachment</c> file explaining what needs to be redone. 409 if no review is currently pending.
    /// </summary>
    [HttpPost("review/rework")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(27 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 27 * 1024 * 1024)]
    [ProducesResponseType(typeof(TaskReviewSummaryDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> RequestRework(long approvalRequestId, [FromForm] ApprovalRequestReworkRequestDto request, CancellationToken ct) =>
        Ok(await _lifecycle.RequestTaskReworkAsync(approvalRequestId,
            new RequestTaskReworkRequestDto { ReviewedById = request.ReviewedById, ReviewedByName = request.ReviewedByName, ReworkRemark = request.ReworkRemark },
            request.Attachment, ct));

    /// <summary>Full review-cycle history, oldest (cycle 1) first. EaTask-based — no WorkflowInstanceId is exposed.</summary>
    [HttpGet("review/history")]
    [ProducesResponseType(typeof(List<TaskReviewHistoryItemDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> ReviewHistory(long approvalRequestId, CancellationToken ct)
        => Ok(await _lifecycle.GetReviewHistoryAsync(approvalRequestId, ct));
}
