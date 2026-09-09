using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/audit")]
public class AuditController : ControllerBase
{
    private readonly EaFmsDbContext _context;

    public AuditController(EaFmsDbContext context)
    {
        _context = context;
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var item = await _context.AuditLogs.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (item is null) return NotFound();
        return Ok(item);
    }

    [HttpGet("entity/{module}/{entityId}")]
    public async Task<IActionResult> GetByEntity(string module, string entityId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.AuditLogs.AsNoTracking().Where(a => a.Module == module && a.EntityId == entityId);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.OccurredAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new { items, total });
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? module = null, [FromQuery] string? entityName = null, [FromQuery] string? entityId = null, [FromQuery] string? actorId = null, [FromQuery] string? actionType = null, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(module)) query = query.Where(a => a.Module == module);
        if (!string.IsNullOrWhiteSpace(entityName)) query = query.Where(a => a.EntityName == entityName);
        if (!string.IsNullOrWhiteSpace(entityId)) query = query.Where(a => a.EntityId == entityId);
        if (!string.IsNullOrWhiteSpace(actorId)) query = query.Where(a => a.ActorId == actorId);
        if (!string.IsNullOrWhiteSpace(actionType)) query = query.Where(a => a.ActionType == actionType);
        if (from.HasValue) query = query.Where(a => a.OccurredAt >= from.Value);
        if (to.HasValue) query = query.Where(a => a.OccurredAt <= to.Value);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.OccurredAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return Ok(new { items, total });
    }
}
