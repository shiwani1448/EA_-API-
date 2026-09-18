using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Controllers;

/// <summary>
/// Minimal read-only view over the existing PriorityLevel master, so frontends (e.g. the
/// Delegation create form) can populate a Priority dropdown instead of hardcoding values.
/// No create/update here — PriorityLevel already has no such API anywhere in this project,
/// and none was asked for; this only fills the missing read gap.
/// </summary>
[ApiController]
[Route("api/ea/priority-levels")]
public sealed class PriorityLevelsController : ControllerBase
{
    private readonly EaFmsDbContext _context;

    public PriorityLevelsController(EaFmsDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<PriorityLevelDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] bool activeOnly = false, CancellationToken ct = default)
    {
        var query = _context.PriorityLevels.AsNoTracking().Where(p => !p.IsDeleted);
        if (activeOnly) query = query.Where(p => p.IsActive);

        var result = await query
            .OrderByDescending(p => p.Level)
            .ThenBy(p => p.Name)
            .Select(p => new PriorityLevelDto
            {
                Id = p.Id,
                Name = p.Name,
                Level = p.Level,
                Description = p.Description,
                IsActive = p.IsActive
            })
            .ToListAsync(ct);

        return Ok(result);
    }
}
