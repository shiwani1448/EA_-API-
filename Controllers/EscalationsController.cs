using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/escalations")]
public class EscalationsController : ControllerBase
{
    private readonly IEscalationService _service;

    public EscalationsController(IEscalationService service)
    {
        _service = service;
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var e = await _service.GetByIdAsync(id, ct);
        return Ok(e);
    }

    [HttpPost("{id:long}/resolve")]
    public async Task<IActionResult> Resolve(long id, [FromBody] ResolveEscalationRequestDto? dto, CancellationToken ct)
    {
        dto ??= new ResolveEscalationRequestDto();
        await _service.ResolveAsync(id, dto, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/acknowledge")]
    public async Task<IActionResult> Acknowledge(long id, [FromBody] AcknowledgeEscalationRequestDto? dto, CancellationToken ct)
    {
        dto ??= new AcknowledgeEscalationRequestDto();
        await _service.AcknowledgeAsync(id, dto?.AcknowledgementNote, ct);
        return NoContent();
    }

    [HttpGet("/api/ea/escalation-levels")]
    public async Task<IActionResult> GetLevels(CancellationToken ct)
    {
        var list = await _service.GetActiveLevelsAsync(ct);
        return Ok(list);
    }
}
