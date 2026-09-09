using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/tat-rules")]
public class TatRulesController : ControllerBase
{
    private readonly EaFmsDbContext _context;

    public TatRulesController(EaFmsDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var list = await _context.TatRules.Where(t => !t.IsDeleted && t.IsActive).ToListAsync(ct);
        return Ok(list.Select(t => new TatRuleDto {
            Id = t.Id,
            BusinessModuleId = t.BusinessModuleId,
            OperationCode = t.OperationCode,
            PriorityLevelId = t.PriorityLevelId,
            Minutes = t.Minutes,
            IsActive = t.IsActive
        }).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TatRuleDto dto, CancellationToken ct)
    {
        var now = Jarvis5.Common.Clock.UtcNowTz;
        var t = new TatRule
        {
            BusinessModuleId = dto.BusinessModuleId,
            OperationCode = dto.OperationCode,
            PriorityLevelId = dto.PriorityLevelId,
            Minutes = dto.Minutes,
            IsActive = dto.IsActive,
            CreatedBy = User?.Identity?.Name ?? string.Empty,
            CreatedDate = now,
            IsDeleted = false
        };

        await _context.TatRules.AddAsync(t, ct);
        await _context.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = t.Id }, t);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] TatRuleDto dto, CancellationToken ct)
    {
        var t = await _context.TatRules.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (t is null) return NotFound();

        t.BusinessModuleId = dto.BusinessModuleId;
        t.OperationCode = dto.OperationCode;
        t.PriorityLevelId = dto.PriorityLevelId;
        t.Minutes = dto.Minutes;
        t.IsActive = dto.IsActive;
        t.ModifiedBy = User?.Identity?.Name ?? string.Empty;
        t.ModifiedDate = Jarvis5.Common.Clock.UtcNowTz;

        _context.TatRules.Update(t);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }
}
