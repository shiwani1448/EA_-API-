using Jarvis5.Data.EaFms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/meetings/kpis")]
public class MeetingsKpisController : ControllerBase
{
    private readonly EaFmsDbContext _context;

    public MeetingsKpisController(EaFmsDbContext context)
    {
        _context = context;
    }

    [HttpGet("/api/ea/meetings/kpis/summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var now = Jarvis5.Common.Clock.UtcNowTz;
        var meetings = _context.Meetings.Where(m => !m.IsDeleted);

        var total = await meetings.CountAsync(ct);
        var completed = await meetings.CountAsync(m => m.CompletedAt != null, ct);
        var pending = await meetings.CountAsync(m => m.CompletedAt == null, ct);
        var today = await meetings.CountAsync(m => m.MeetingDate != null && m.MeetingDate.Value.Date == now.Date, ct);

        // Minutes pending: meeting completed or started and no submitted minutes
        var minutesPending = await meetings.Where(m => (m.CompletedAt != null || (m.StartDateTime != null && m.StartDateTime <= now)))
            .CountAsync(m => !_context.MeetingMinutes.Any(mm => !mm.IsDeleted && mm.MeetingId == m.Id && mm.SubmittedAt != null), ct);

        var overdueActions = await _context.MeetingActions.CountAsync(a => !a.IsDeleted && a.CompletedAt == null && a.DueDate != null && a.DueDate < now, ct);

        return Ok(new {
            Total = total,
            Pending = pending,
            Completed = completed,
            Today = today,
            MinutesPending = minutesPending,
            OverdueActions = overdueActions
        });
    }

    [HttpGet("/api/ea/meetings/kpis/total")]
    public async Task<IActionResult> Total(CancellationToken ct)
    {
        var total = await _context.Meetings.CountAsync(m => !m.IsDeleted, ct);
        return Ok(new { Total = total });
    }

    [HttpGet("/api/ea/meetings/kpis/pending")]
    public async Task<IActionResult> Pending(CancellationToken ct)
    {
        var pending = await _context.Meetings.CountAsync(m => !m.IsDeleted && m.CompletedAt == null, ct);
        return Ok(new { Pending = pending });
    }

    [HttpGet("/api/ea/meetings/kpis/completed")]
    public async Task<IActionResult> Completed(CancellationToken ct)
    {
        var completed = await _context.Meetings.CountAsync(m => !m.IsDeleted && m.CompletedAt != null, ct);
        return Ok(new { Completed = completed });
    }

    [HttpGet("/api/ea/meetings/kpis/today")]
    public async Task<IActionResult> Today(CancellationToken ct)
    {
        var now = Jarvis5.Common.Clock.UtcNowTz;
        var today = await _context.Meetings.CountAsync(m => !m.IsDeleted && m.MeetingDate != null && m.MeetingDate.Value.Date == now.Date, ct);
        return Ok(new { Today = today });
    }

    [HttpGet("/api/ea/meetings/kpis/minutes-pending")]
    public async Task<IActionResult> MinutesPending(CancellationToken ct)
    {
        var now = Jarvis5.Common.Clock.UtcNowTz;
        var count = await _context.Meetings.Where(m => !m.IsDeleted && (m.CompletedAt != null || (m.StartDateTime != null && m.StartDateTime <= now)))
            .CountAsync(m => !_context.MeetingMinutes.Any(mm => !mm.IsDeleted && mm.MeetingId == m.Id && mm.SubmittedAt != null), ct);
        return Ok(new { MinutesPending = count });
    }

    [HttpGet("/api/ea/meetings/kpis/overdue-actions")]
    public async Task<IActionResult> OverdueActions(CancellationToken ct)
    {
        var now = Jarvis5.Common.Clock.UtcNowTz;
        var count = await _context.MeetingActions.CountAsync(a => !a.IsDeleted && a.CompletedAt == null && a.DueDate != null && a.DueDate < now, ct);
        return Ok(new { OverdueActions = count });
    }
}
