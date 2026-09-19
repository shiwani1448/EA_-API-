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

    /// <summary>Create a new Escalation tied to a Followup.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EscalationResponseDto), 201)]
    public async Task<IActionResult> Create([FromBody] CreateEscalationRequestDto dto, CancellationToken ct)
    {
        var result = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Lists a followup's historical escalations, or the central paginated escalation register.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] EscalationListQueryDto query, CancellationToken ct)
    {
        if (query.FollowupId.HasValue)
        {
            if (query.FollowupId.Value <= 0)
                return BadRequest(new { error = "followupId query parameter must be > 0." });
            return Ok(await _service.GetByFollowupIdAsync(query.FollowupId.Value, ct));
        }
        return Ok(await _service.GetPagedAsync(query, ct));
    }
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(EscalationResponseDto), 200)]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var e = await _service.GetByIdAsync(id, ct);
        return Ok(e);
    }

    /// <summary>Resolve (close) an open or acknowledged escalation.</summary>
    [HttpPost("{id:long}/resolve")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Resolve(long id, [FromBody] ResolveEscalationRequestDto? dto, CancellationToken ct)
    {
        dto ??= new ResolveEscalationRequestDto();
        await _service.ResolveAsync(id, dto, ct);
        return NoContent();
    }

    /// <summary>Acknowledge an open escalation.</summary>
    [HttpPost("{id:long}/acknowledge")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Acknowledge(long id, [FromBody] AcknowledgeEscalationRequestDto? dto, CancellationToken ct)
    {
        dto ??= new AcknowledgeEscalationRequestDto();
        await _service.AcknowledgeAsync(id, dto?.AcknowledgementNote, ct);
        return NoContent();
    }

    /// <summary>List all active (non-deleted) escalation levels, ordered by level number.</summary>
    [HttpGet("/api/ea/escalation-levels")]
    [ProducesResponseType(typeof(List<EscalationLevelResponseDto>), 200)]
    public async Task<IActionResult> GetLevels(CancellationToken ct)
    {
        var list = await _service.GetActiveLevelsAsync(ct);
        return Ok(list);
    }
}
