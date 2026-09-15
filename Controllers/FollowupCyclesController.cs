using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/followups/{followupId:long}/cycles")]
public class FollowupCyclesController : ControllerBase
{
    private readonly IFollowupCycleService _service;
    public FollowupCyclesController(IFollowupCycleService service) => _service = service;

    [HttpPost]
    public async Task<ActionResult<FollowupCycleResponseDto>> Create(long followupId, [FromBody] CreateFollowupCycleRequestDto? dto, CancellationToken ct)
    {
        dto ??= new CreateFollowupCycleRequestDto();
        var cycle = await _service.CreateAsync(followupId, dto, ct);
        return CreatedAtAction(nameof(Get), new { followupId, cycleId = cycle.Id }, cycle);
    }

    [HttpGet]
    public async Task<ActionResult<List<FollowupCycleResponseDto>>> GetHistory(long followupId, CancellationToken ct) =>
        Ok(await _service.GetHistoryAsync(followupId, ct));

    [HttpGet("{cycleId:long}")]
    public async Task<ActionResult<FollowupCycleResponseDto>> Get(long followupId, long cycleId, CancellationToken ct) =>
        Ok(await _service.GetAsync(followupId, cycleId, ct));
}
