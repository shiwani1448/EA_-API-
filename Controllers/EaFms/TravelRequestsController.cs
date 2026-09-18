using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers.EaFms;

/// <summary>
/// Travel CRUD and submission/approval APIs.
///
/// Routes:
///   POST   /api/ea/travel/requests
///   GET    /api/ea/travel/requests/{travelRequestId}
///   PUT    /api/ea/travel/requests/{travelRequestId}
///   GET    /api/ea/travel/requests
///   POST   /api/ea/travel/requests/{travelRequestId}/start
///   POST   /api/ea/travel/requests/{travelRequestId}/complete
///   POST   /api/ea/travel/requests/{travelRequestId}/cancel
///   GET    /api/ea/travel/requests/{travelRequestId}/history
///
/// NOT implemented — deferred (not part of the Travel business lifecycle established
/// by the frontend for this step):
///   /pause, /resume
/// </summary>
[ApiController]
[Route("api/ea/travel/requests")]
public class TravelRequestsController : ControllerBase
{
    private readonly ITravelRequestService _service;

    public TravelRequestsController(ITravelRequestService service)
    {
        _service = service;
    }

    /// <summary>
    /// Create a new Travel Request draft.
    /// Creates the TravelRequest and its required central EaTask (no TAT — Travel has
    /// no approved TAT classification yet) atomically. Returns 409 if the "Travel &amp;
    /// Hospitality" BusinessModule is not configured.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TravelRequestCreatedDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTravelRequestDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateDraftAsync(dto, cancellationToken);
        return CreatedAtAction(
            nameof(GetById),
            new { travelRequestId = result.TravelRequestId },
            result);
    }

    /// <summary>
    /// Get a Travel Request by id.
    /// </summary>
    [HttpGet("{travelRequestId:long}")]
    [ProducesResponseType(typeof(TravelRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        long travelRequestId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(travelRequestId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Update a Travel Request draft.
    /// Pre-submission draft edit, or controlled ChangesRequested rework with expectedCycleNo.
    /// </summary>
    [HttpPut("{travelRequestId:long}")]
    [ProducesResponseType(typeof(TravelRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateDraft(
        long travelRequestId,
        [FromBody] UpdateTravelDraftDto dto,
        CancellationToken cancellationToken,
        [FromQuery, System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] int? expectedCycleNo = null)
    {
        var result = await _service.UpdateDraftAsync(travelRequestId, dto, cancellationToken, expectedCycleNo);
        return Ok(result);
    }

    [HttpPost("{travelRequestId:long}/submit")]
    [ProducesResponseType(typeof(TravelActionResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Submit(long travelRequestId, CancellationToken ct) =>
        Ok(await _service.SubmitAsync(travelRequestId, ct));

    [HttpPost("{travelRequestId:long}/approve")]
    [ProducesResponseType(typeof(TravelActionResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(long travelRequestId, [FromBody] ApproveTravelRequestDto dto, CancellationToken ct) =>
        Ok(await _service.ApproveAsync(travelRequestId, dto, ct));

    [HttpPost("{travelRequestId:long}/reject")]
    [ProducesResponseType(typeof(TravelActionResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(long travelRequestId, [FromBody] RejectTravelRequestDto dto, CancellationToken ct) =>
        Ok(await _service.RejectAsync(travelRequestId, dto, ct));

    [HttpPost("{travelRequestId:long}/request-changes")]
    [ProducesResponseType(typeof(TravelActionResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RequestChanges(long travelRequestId, [FromBody] RequestTravelChangesDto dto, CancellationToken ct) =>
        Ok(await _service.RequestChangesAsync(travelRequestId, dto, ct));

    [HttpPost("{travelRequestId:long}/resubmit")]
    [ProducesResponseType(typeof(TravelActionResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resubmit(long travelRequestId, [FromBody] ResubmitTravelRequestDto dto, CancellationToken ct) =>
        Ok(await _service.ResubmitAsync(travelRequestId, dto, ct));

    /// <summary>Upcoming -&gt; Active. Bodyless.</summary>
    [HttpPost("{travelRequestId:long}/start")]
    [ProducesResponseType(typeof(TravelActionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start(long travelRequestId, CancellationToken ct) =>
        Ok(await _service.StartAsync(travelRequestId, ct));

    /// <summary>Active -&gt; Completed. Bodyless.</summary>
    [HttpPost("{travelRequestId:long}/complete")]
    [ProducesResponseType(typeof(TravelActionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(long travelRequestId, CancellationToken ct) =>
        Ok(await _service.CompleteAsync(travelRequestId, ct));

    /// <summary>Business cancellation. Bodyless — no cancellation-reason input exists in the current frontend contract.</summary>
    [HttpPost("{travelRequestId:long}/cancel")]
    [ProducesResponseType(typeof(TravelActionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(long travelRequestId, CancellationToken ct) =>
        Ok(await _service.CancelAsync(travelRequestId, ct));

    /// <summary>
    /// Complete chronological Travel timeline (oldest to newest), read from the shared
    /// audit infrastructure. Never writes a new audit record.
    /// </summary>
    [HttpGet("{travelRequestId:long}/history")]
    [ProducesResponseType(typeof(List<TravelHistoryEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> History(long travelRequestId, CancellationToken ct) =>
        Ok(await _service.GetHistoryAsync(travelRequestId, ct));

    /// <summary>
    /// List/search/filter Travel Requests with pagination.
    /// Supports explicit businessState and approvalState filters (not a single "status").
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(Common.PagedResult<TravelRequestListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? businessState,
        [FromQuery] string? approvalState,
        [FromQuery] string? priority,
        [FromQuery] string? travellerName,
        [FromQuery] string? department,
        [FromQuery] string? createdBy,
        [FromQuery] string? approverId,
        [FromQuery] string? referenceNo,
        [FromQuery] DateTime? requiredDateFrom,
        [FromQuery] DateTime? requiredDateTo,
        [FromQuery] DateTime? departureDateFrom,
        [FromQuery] DateTime? departureDateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new TravelRequestListQueryDto
        {
            Search = search,
            BusinessState = businessState,
            ApprovalState = approvalState,
            Priority = priority,
            TravellerName = travellerName,
            Department = department,
            CreatedBy = createdBy,
            ApproverId = approverId,
            ReferenceNo = referenceNo,
            RequiredDateFrom = requiredDateFrom,
            RequiredDateTo = requiredDateTo,
            DepartureDateFrom = departureDateFrom,
            DepartureDateTo = departureDateTo,
            Page = page,
            PageSize = pageSize
        };

        var result = await _service.ListAsync(query, cancellationToken);
        return Ok(result);
    }
}
