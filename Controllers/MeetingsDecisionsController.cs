using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/meetings/{meetingId:long}/decisions")]
public class MeetingsDecisionsController : ControllerBase
{
    private readonly EaFmsDbContext _context;

    public MeetingsDecisionsController(EaFmsDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get(long meetingId, CancellationToken ct)
    {
        var m = await _context.Meetings.FirstOrDefaultAsync(x => x.Id == meetingId && !x.IsDeleted, ct);
        if (m is null) return NotFound();

        var list = await _context.MeetingDecisions.Where(d => d.MeetingId == meetingId && !d.IsDeleted).ToListAsync(ct);
        return Ok(list.Select(d => new MeetingDecisionDto {
            Id = d.Id,
            Decision = d.Decision,
            OwnerName = d.OwnerName,
            DecisionDate = d.DecisionDate,
            DueDate = d.DueDate,
            Status = d.Status
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create(long meetingId, [FromBody] CreateMeetingDecisionDto dto, CancellationToken ct)
    {
        var m = await _context.Meetings.FirstOrDefaultAsync(x => x.Id == meetingId && !x.IsDeleted, ct);
        if (m is null) return NotFound();

        var now = Jarvis5.Common.Clock.UtcNowTz;
        var d = new MeetingDecision
        {
            MeetingId = meetingId,
            Decision = dto.Decision,
            OwnerName = dto.OwnerName,
            DecisionDate = dto.DecisionDate,
            DueDate = dto.DueDate,
            Status = dto.Status,
            CreatedBy = User?.Identity?.Name ?? string.Empty,
            CreatedDate = now,
            IsDeleted = false
        };

        await _context.MeetingDecisions.AddAsync(d, ct);
        await _context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { meetingId }, d);
    }
}
