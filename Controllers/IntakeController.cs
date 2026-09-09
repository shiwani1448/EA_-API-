using AutoMapper;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/intake")]
public class IntakeController : ControllerBase
{
    private readonly IIntakeService _service;

    public IntakeController(IIntakeService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateIntakeRequestDto dto, CancellationToken ct)
    {
        var created = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        return Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var (items, total) = await _service.GetPagedAsync(pageNumber, pageSize, search, ct);
        return Ok(new { items, total });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateIntakeRequestDto dto, CancellationToken ct)
    {
        var updated = await _service.UpdateAsync(id, dto, ct);
        return Ok(updated);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    // Classifications
    [HttpPost("{intakeId:long}/classifications")]
    public async Task<IActionResult> CreateClassification(long intakeId, [FromBody] CreateIntakeClassificationDto dto, CancellationToken ct)
    {
        var created = await _service.CreateClassificationAsync(intakeId, dto, ct);
        return CreatedAtAction(nameof(GetClassifications), new { intakeId }, created);
    }

    [HttpGet("{intakeId:long}/classifications")]
    public async Task<IActionResult> GetClassifications(long intakeId, CancellationToken ct)
    {
        var list = await _service.GetClassificationsAsync(intakeId, ct);
        return Ok(list);
    }

    [HttpPut("{intakeId:long}/classifications/{classificationId:long}")]
    public async Task<IActionResult> UpdateClassification(long intakeId, long classificationId, [FromBody] UpdateIntakeClassificationDto dto, CancellationToken ct)
    {
        var updated = await _service.UpdateClassificationAsync(intakeId, classificationId, dto, ct);
        return Ok(updated);
    }

    [HttpDelete("{intakeId:long}/classifications/{classificationId:long}")]
    public async Task<IActionResult> DeleteClassification(long intakeId, long classificationId, CancellationToken ct)
    {
        await _service.DeleteClassificationAsync(intakeId, classificationId, ct);
        return NoContent();
    }
}
