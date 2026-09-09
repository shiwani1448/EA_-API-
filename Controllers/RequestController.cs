using Jarvis5.Common;
using Jarvis5.Dtos;
using Jarvis5.Services;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/request")]
public class RequestController : ControllerBase
{
    private readonly IRequestService _requestService;

    public RequestController(IRequestService requestService)
    {
        _requestService = requestService;
    }

    /// <summary>Raise a new SCIH improvement/automation request (Stage 1).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(RequestDetailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<RequestDetailDto>> Create([FromBody] CreateRequestDto dto, CancellationToken ct)
    {
        var result = await _requestService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Update a request. Only allowed while Status = RAISED.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(RequestDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RequestDetailDto>> Update(long id, [FromBody] UpdateRequestDto dto, CancellationToken ct)
    {
        var result = await _requestService.UpdateAsync(id, dto, ct);
        return Ok(result);
    }

    /// <summary>Partially update a request's overall start/end dates. Only non-null
    /// fields in the body are changed.</summary>
    [HttpPatch("{id:long}/overall-dates")]
    [ProducesResponseType(typeof(RequestDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RequestDetailDto>> UpdateOverallDates(long id, [FromBody] UpdateRequestOverallDatesDto dto, CancellationToken ct)
    {
        var result = await _requestService.UpdateOverallDatesAsync(id, dto, ct);
        return Ok(result);
    }

    /// <summary>List requests with optional filters and paging.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<RequestListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<RequestListItemDto>>> GetAll([FromQuery] RequestFilterDto filter, CancellationToken ct)
    {
        var result = await _requestService.GetPagedAsync(filter, ct);
        return Ok(result);
    }

    /// <summary>Get full request details: metadata, pain points, attachments, meta data, latest history.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(RequestDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RequestDetailDto>> GetById(long id, CancellationToken ct)
    {
        var result = await _requestService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    /// <summary>Complete timeline of a request, sorted by ActionDate.</summary>
    [HttpGet("{id:long}/history")]
    [ProducesResponseType(typeof(List<RequestHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RequestHistoryDto>>> GetHistory(long id, CancellationToken ct)
    {
        var result = await _requestService.GetHistoryAsync(id, ct);
        return Ok(result);
    }

    /// <summary>Soft delete a request. Recorded as a "Request Deleted" history entry.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _requestService.DeleteAsync(id, ct);
        return NoContent();
    }
}
