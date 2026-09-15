using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/tat-rules")]
public class TatRulesController(ITatRuleService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] long? businessModuleId, [FromQuery] string? type, [FromQuery] string? subtype, CancellationToken ct)
    {
        if (businessModuleId.HasValue && businessModuleId <= 0)
            return Problem(statusCode: 400, detail: "BusinessModuleId must be positive.");
        return Ok(await service.QueryAsync(businessModuleId, type, subtype, ct));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    // Authorization policy for configuration writes will be added in a later phase.
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveTatRuleDto? dto, CancellationToken ct)
    {
        dto ??= new SaveTatRuleDto();
        var rule = await service.SaveAsync(null, dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = rule.Id }, rule);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveTatRuleDto? dto, CancellationToken ct)
    {
        dto ??= new SaveTatRuleDto();
        await service.SaveAsync(id, dto, ct);
        return NoContent();
    }
}
