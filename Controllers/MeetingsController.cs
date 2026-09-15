using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/meetings")]
public class MeetingsController : ControllerBase
{
    private readonly IMeetingService _service;

    public MeetingsController(IMeetingService service)
    {
        _service = service;
    }

    [HttpPost]
    [ProducesResponseType(typeof(MeetingDetailResponseDto), 201)]
    public async Task<IActionResult> Create([FromBody] CreateMeetingRequestDto? dto, CancellationToken ct)
    {
        dto ??= new CreateMeetingRequestDto();
        var m = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = m.MeetingId }, m);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<MeetingListItemResponseDto>), 200)]
    public async Task<IActionResult> Query([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var list = await _service.QueryAsync(search, page, pageSize, ct);
        return Ok(list);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(MeetingDetailResponseDto), 200)]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var m = await _service.GetByIdAsync(id, ct);
        return Ok(m);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateMeetingRequestDto? dto, CancellationToken ct)
    {
        dto ??= new UpdateMeetingRequestDto();
        await _service.UpdateAsync(id, dto, ct);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
