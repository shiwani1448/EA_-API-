using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers.EaFms;

/// <summary>
/// Travel Request CRUD and read APIs (Step 3).
///
/// Routes:
///   POST   /api/ea/travel/requests
///   GET    /api/ea/travel/requests/{travelRequestId}
///   PUT    /api/ea/travel/requests/{travelRequestId}
///   GET    /api/ea/travel/requests
///
/// NOT implemented in this step:
///   /submit, /approve, /reject, /request-changes, /resubmit,
///   /start, /pause, /resume, /complete, /cancel
/// </summary>
[ApiController]
[Route("api/ea/travel/requests")]
[Authorize]
public class TravelRequestsController : ControllerBase
{
    private readonly ITravelRequestService _service;

    public TravelRequestsController(ITravelRequestService service)
    {
        _service = service;
    }

    /// <summary>
    /// Create a new Travel Request draft.
    ///
    /// ⚠ BLOCKED — TRAVEL EATASK/TAT CREATION POLICY REQUIRES DECISION
    /// The endpoint is wired; the service will throw a 409 with a precise
    /// message until the TAT/task-creation policy for Travel is approved.
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
    /// Only allowed while BusinessState == "Draft".
    /// </summary>
    [HttpPut("{travelRequestId:long}")]
    [ProducesResponseType(typeof(TravelRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateDraft(
        long travelRequestId,
        [FromBody] UpdateTravelDraftDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _service.UpdateDraftAsync(travelRequestId, dto, cancellationToken);
        return Ok(result);
    }

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
