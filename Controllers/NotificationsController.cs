using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _service;

    public NotificationsController(INotificationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var (items, total) = await _service.GetForCurrentUserAsync(page, pageSize, ct);
        return Ok(new { items, total });
    }

    [HttpGet("unread")]
    public async Task<IActionResult> GetUnread([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var (items, total) = await _service.GetUnreadForCurrentUserAsync(page, pageSize, ct);
        return Ok(new { items, total });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var item = await _service.GetByIdForCurrentUserAsync(id, ct);
        if (item is null) return NotFound();
        return Ok(item);
    }

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct)
    {
        var item = await _service.MarkReadAsync(id, ct);
        if (item is null) return NotFound();
        return Ok(item);
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var count = await _service.MarkAllReadAsync(ct);
        return Ok(new { count });
    }

    // POST /api/ea/notifications (explicit create) is intentionally NOT exposed here
    // because there is no existing safe authorization policy for public creation.
}
