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
    /// Complete an InProgress Delegation (InProgress -> Completed). 409 if not currently InProgress or while an open pause exists (resume first).
    /// multipart/form-data with an optional <c>completionPdf</c> file (same field name as Meeting complete).
    /// Unlike Meeting the PDF is optional. When supplied it must be a valid PDF (max 25 MiB); it is stored in
    /// the same transaction as the completion, so a failure leaves the Delegation and its EaTask unchanged.
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
}
