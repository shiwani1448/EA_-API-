using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/business-modules")]
public sealed class BusinessModulesController(IBusinessModuleService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] bool activeOnly = false, CancellationToken ct = default) =>
        Ok(await service.GetAllAsync(activeOnly, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct) => Ok(await service.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(typeof(BusinessModuleDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] SaveBusinessModuleDto? dto, CancellationToken ct)
    {
        var module = await service.CreateAsync(dto ?? new SaveBusinessModuleDto(), ct);
        return CreatedAtAction(nameof(GetById), new { id = module.Id }, module);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(BusinessModuleDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long id, [FromBody] SaveBusinessModuleDto? dto, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, dto ?? new SaveBusinessModuleDto(), ct));
}
