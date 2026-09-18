using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/tasks")]
public class EaTasksController(IEaTaskService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] long? businessModuleId,
        [FromQuery] string? businessRecordId, CancellationToken ct)
    {
        if (businessModuleId.HasValue && businessModuleId <= 0)
            return Problem(statusCode: 400, detail: "BusinessModuleId must be positive.");
        if (businessRecordId is not null && (string.IsNullOrWhiteSpace(businessRecordId) || businessRecordId.Trim().Length > 200))
            return Problem(statusCode: 400, detail: "BusinessRecordId must contain 1 to 200 characters.");
        return Ok(await service.QueryAsync(businessModuleId, businessRecordId, ct));
    }

    [HttpGet("{eaTaskId:long}")]
    public async Task<IActionResult> GetById(long eaTaskId, CancellationToken ct) =>
        Ok(await service.GetAsync(eaTaskId, ct));

    [HttpGet("{eaTaskId:long}/history")]
    public async Task<IActionResult> GetHistory(long eaTaskId, CancellationToken ct) =>
        Ok(await service.GetHistoryAsync(eaTaskId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEaTaskDto? dto, CancellationToken ct)
    {
        dto ??= new CreateEaTaskDto();
        var task = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { eaTaskId = task.EaTaskId }, task);
    }
}
