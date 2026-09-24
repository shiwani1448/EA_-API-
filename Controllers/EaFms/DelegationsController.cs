using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers.EaFms;

/// <summary>
/// Delegation CRUD, register and lifecycle APIs (Steps 2-4).
///
/// Routes:
///   POST   /api/ea/delegations
///   GET    /api/ea/delegations
///   GET    /api/ea/delegations/summary
///   GET    /api/ea/delegations/{delegationId}
///   PUT    /api/ea/delegations/{delegationId}
///   POST   /api/ea/delegations/{delegationId}/start
///   POST   /api/ea/delegations/{delegationId}/pause      (optional JSON body: pauseReason)
///   POST   /api/ea/delegations/{delegationId}/resume     (no body)
///   POST   /api/ea/delegations/{delegationId}/complete   (multipart/form-data; completionPdf optional)
///   POST   /api/ea/delegations/{delegationId}/review/start   (starts the reviewer's TAT clock for the open Review phase)
///   POST   /api/ea/delegations/{delegationId}/rework/start   (starts the doer's TAT clock for the open Rework phase)
///
/// NOT implemented yet: /cancel, /history, /reminder, /escalation.
/// </summary>
[ApiController]
[Route("api/ea/delegations")]
public class DelegationsController : ControllerBase
{
    private readonly IDelegationService _service;

    public DelegationsController(IDelegationService service)
    {
        _service = service;
    }

    /// <summary>
    /// Create a new Delegation. Creates the Delegation and its required central EaTask
    /// (no TAT — Delegation uses an explicit DueDate) atomically. Returns 409 if the
    /// "Delegation" BusinessModule is not configured, or SourceBusinessModuleId is invalid.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DelegationResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] DelegationCreateRequestDto dto, CancellationToken ct)
    {
        var result = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { delegationId = result.DelegationId }, result);
    }

    /// <summary>List/search/filter Delegations with pagination for the register/KPI tabs.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(Common.PagedResult<DelegationResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] DelegationListQueryDto query, CancellationToken ct) =>
        Ok(await _service.ListAsync(query, ct));

    /// <summary>Register KPI card counts (total/pending/inProgress/dueToday/overdue/completed).</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DelegationSummaryResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken ct) =>
        Ok(await _service.GetSummaryAsync(ct));

    /// <summary>Get a Delegation by id.</summary>
    [HttpGet("{delegationId:long}")]
    [ProducesResponseType(typeof(DelegationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(long delegationId, CancellationToken ct) =>
        Ok(await _service.GetByIdAsync(delegationId, ct));

    /// <summary>Update a Delegation's editable business fields. Blocked (409) once Completed.</summary>
    [HttpPut("{delegationId:long}")]
    [ProducesResponseType(typeof(DelegationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(long delegationId, [FromBody] DelegationUpdateRequestDto dto, CancellationToken ct) =>
        Ok(await _service.UpdateAsync(delegationId, dto, ct));

    /// <summary>Start a Pending Delegation (Pending -> InProgress). No request body. 409 if not currently Pending.</summary>
    [HttpPost("{delegationId:long}/start")]
    [ProducesResponseType(typeof(DelegationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start(long delegationId, CancellationToken ct) =>
        Ok(await _service.StartAsync(delegationId, ct));

    /// <summary>
    /// Pause an InProgress Delegation. Status stays InProgress; one shared WorkPause is opened and the response
    /// carries the derived <c>isPaused=true</c>. Optional body <c>{ "pauseReason": "..." }</c>.
    /// 404 unknown id; 409 when not InProgress (Pending/Completed) or already paused.
    /// </summary>
    [HttpPost("{delegationId:long}/pause")]
    [ProducesResponseType(typeof(DelegationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Pause(long delegationId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] DelegationPauseRequestDto? request,
        CancellationToken ct) =>
        Ok(await _service.PauseAsync(delegationId, request, ct));

    /// <summary>
    /// Resume a paused Delegation (closes the open WorkPause; <c>isPaused=false</c>). No request body.
    /// 404 unknown id; 409 when not InProgress or not currently paused.
    /// </summary>
    [HttpPost("{delegationId:long}/resume")]
    [ProducesResponseType(typeof(DelegationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resume(long delegationId, CancellationToken ct) =>
        Ok(await _service.ResumeAsync(delegationId, ct));

    /// <summary>
    /// Doer's "I'm done" action. Despite the name this does not finalize the Delegation (see
    /// review/approve below) — it uploads the optional completion PDF and opens a Task Review cycle,
    /// leaving the Delegation InProgress until the assignee approves it. 409 if not currently
    /// InProgress, while an open pause exists (resume first), or if a review is already pending.
    /// multipart/form-data with an optional <c>completionPdf</c> file (same field name as Meeting complete).
    /// Unlike Meeting the PDF is optional. When supplied it must be a valid PDF (max 25 MiB); it is stored in
    /// the same transaction as the review submission, so a failure leaves the Delegation and its EaTask unchanged.
    /// </summary>
    [HttpPost("{delegationId:long}/complete")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(27 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 27 * 1024 * 1024)]
    [ProducesResponseType(typeof(DelegationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(long delegationId, [FromForm] DelegationCompleteRequestDto request, CancellationToken ct) =>
        Ok(await _service.CompleteAsync(delegationId, request.CompletionPdf, ct));

    // ============================================================
    // TASK REVIEW / REWORK (Phase 1)
    // ============================================================

    /// <summary>
    /// Starts the reviewer's own TAT clock for the currently open Review phase. Complete/
    /// submit-for-review open the Review phase idle now (no auto-start); a reviewer must call this
    /// before their SLA clock begins accruing. No request body. 404 unknown id; 409 if not InProgress,
    /// paused, there's no currently open phase, the open phase isn't Review, or it was already started.
    /// </summary>
    [HttpPost("{delegationId:long}/review/start")]
    [ProducesResponseType(typeof(DelegationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartReview(long delegationId, CancellationToken ct) =>
        Ok(await _service.StartReviewAsync(delegationId, ct));

    /// <summary>
    /// Starts the doer's own TAT clock for the currently open Rework phase. review/rework opens the
    /// Rework phase idle now (no auto-start); the doer must call this before their redo clock begins
    /// accruing. No request body. 404 unknown id; 409 if not InProgress, paused, there's no currently
    /// open phase, the open phase isn't Rework, or it was already started.
    /// </summary>
    [HttpPost("{delegationId:long}/rework/start")]
    [ProducesResponseType(typeof(DelegationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartRework(long delegationId, CancellationToken ct) =>
        Ok(await _service.StartReworkAsync(delegationId, ct));

    /// <summary>Close the current Actual/Rework phase and open the next Review phase atomically. 409 if not InProgress, paused, already pending review, or phase history is inconsistent.</summary>
    [HttpPost("{delegationId:long}/submit-for-review")]
    [ProducesResponseType(typeof(DelegationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitForReview(long delegationId, [FromBody] SubmitForReviewRequestDto? dto, CancellationToken ct) =>
        Ok(await _service.SubmitForReviewAsync(delegationId, dto ?? new SubmitForReviewRequestDto(), ct));

    /// <summary>
    /// Approve the current pending review cycle. This is what actually finalizes the Delegation as
    /// Completed (freezes TAT, closes any pause anchor) — Complete only opened the review cycle.
    /// multipart/form-data with an optional <c>attachment</c> file so the assignee can attach their
    /// own document (sign-off notes, an annotated file) to the approval. 409 if no review is pending.
    /// </summary>
    [HttpPost("{delegationId:long}/review/approve")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(27 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 27 * 1024 * 1024)]
    [ProducesResponseType(typeof(DelegationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveReview(long delegationId, [FromForm] DelegationApproveReviewRequestDto request, CancellationToken ct) =>
        Ok(await _service.ApproveReviewAsync(delegationId,
            new ApproveTaskReviewRequestDto { ReviewedById = request.ReviewedById, ReviewedByName = request.ReviewedByName, ReviewRemark = request.ReviewRemark },
            request.Attachment, ct));

    /// <summary>
    /// Send the current pending review cycle back for rework. No Delegation/EaTask state change —
    /// it is still InProgress, since Complete never marked it Completed in the first place; the doer
    /// simply calls Complete again once the rework is done, opening the next review cycle.
    /// multipart/form-data with an optional <c>attachment</c> file so the assignee can attach their
    /// own document (marked-up feedback, a reference file) explaining what needs to be redone.
    /// 409 if no review is currently pending.
    /// </summary>
    [HttpPost("{delegationId:long}/review/rework")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(27 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 27 * 1024 * 1024)]
    [ProducesResponseType(typeof(DelegationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestRework(long delegationId, [FromForm] DelegationRequestReworkRequestDto request, CancellationToken ct) =>
        Ok(await _service.RequestReworkAsync(delegationId,
            new RequestTaskReworkRequestDto { ReviewedById = request.ReviewedById, ReviewedByName = request.ReviewedByName, ReworkRemark = request.ReworkRemark },
            request.Attachment, ct));

    /// <summary>Full review-cycle history, oldest (cycle 1) first. EaTask-based — no WorkflowInstanceId is exposed.</summary>
    [HttpGet("{delegationId:long}/review/history")]
    [ProducesResponseType(typeof(List<TaskReviewHistoryItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewHistory(long delegationId, CancellationToken ct) =>
        Ok(await _service.GetReviewHistoryAsync(delegationId, ct));
}
