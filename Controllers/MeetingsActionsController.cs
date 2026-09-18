using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/meetings/{meetingId:long}/actions")]
public class MeetingsActionsController : ControllerBase
{
    private readonly EaFmsDbContext _context;

    public MeetingsActionsController(EaFmsDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get(long meetingId, CancellationToken ct)
    {
        var m = await _context.Meetings.FirstOrDefaultAsync(x => x.Id == meetingId && !x.IsDeleted, ct);
        if (m is null) return NotFound();

        var list = await _context.MeetingActions.Where(a => a.MeetingId == meetingId && !a.IsDeleted).ToListAsync(ct);
        var priorities = await _context.PriorityLevels.ToListAsync(ct);

        var now = Jarvis5.Common.Clock.UtcNowTz;
        var result = list.Select(a => new MeetingActionDto {
            Id = a.Id,
            Title = a.Title,
            Description = a.Description,
            AssignedToId = a.AssignedToId,
            OwnerName = a.OwnerName,
            PriorityLevelId = a.PriorityLevelId,
            PriorityLevelName = a.PriorityLevelId.HasValue ? priorities.FirstOrDefault(p => p.Id == a.PriorityLevelId.Value)?.Name : null,
            DueDate = a.DueDate,
            Status = a.Status,
            IsOverdue = a.CompletedAt == null && a.DueDate != null && a.DueDate < now
        }).ToList();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(long meetingId, [FromBody] CreateMeetingActionDto? dto, CancellationToken ct)
    {
        dto ??= new CreateMeetingActionDto();
        var m = await _context.Meetings.FirstOrDefaultAsync(x => x.Id == meetingId && !x.IsDeleted, ct);
        if (m is null) return NotFound();

        var now = Jarvis5.Common.Clock.UtcNowTz;
        var a = new MeetingAction
        {
            MeetingId = meetingId,
            Title = dto.Title,
            Description = dto.Description,
            AssignedToId = string.IsNullOrWhiteSpace(dto.AssignedToId) ? null : dto.AssignedToId.Trim(),
            OwnerName = dto.OwnerName,
            PriorityLevelId = dto.PriorityLevelId,
            DueDate = dto.DueDate,
            Status = dto.Status,
            CreatedBy = User?.Identity?.Name ?? string.Empty,
            CreatedDate = now,
            IsDeleted = false
        };

        await _context.MeetingActions.AddAsync(a, ct);
        await _context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { meetingId }, a);
    }
}
