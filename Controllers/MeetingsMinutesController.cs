using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/meetings/{meetingId:long}/minutes")]
public class MeetingsMinutesController : ControllerBase
{
    private readonly EaFmsDbContext _context;

    public MeetingsMinutesController(EaFmsDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get(long meetingId, CancellationToken ct)
    {
        var m = await _context.Meetings.FirstOrDefaultAsync(x => x.Id == meetingId && !x.IsDeleted, ct);
        if (m is null) return NotFound();

        var minutes = await _context.MeetingMinutes.Where(x => x.MeetingId == meetingId && !x.IsDeleted).OrderByDescending(x => x.ModifiedDate).ToListAsync(ct);
        return Ok(minutes.Select(x => new MeetingMinutesDto {
            Id = x.Id,
            Summary = x.Summary,
            Notes = x.DiscussionNotes,
            PreparedByName = x.PreparedByName,
            PreparedAt = x.PreparedAt,
            Status = x.Status,
            SubmittedAt = x.SubmittedAt,
            RevisionNo = x.RevisionNumber
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create(long meetingId, [FromBody] CreateMeetingMinutesDto? dto, CancellationToken ct)
    {
        dto ??= new CreateMeetingMinutesDto();
        var m = await _context.Meetings.FirstOrDefaultAsync(x => x.Id == meetingId && !x.IsDeleted, ct);
        if (m is null) return NotFound();

        var now = Jarvis5.Common.Clock.UtcNowTz;
        var mm = new MeetingMinutes
        {
            MeetingId = meetingId,
            Summary = dto.Summary,
            DiscussionNotes = dto.Notes,
            PreparedByName = dto.PreparedByName,
            PreparedAt = dto.PreparedAt,
            Status = "Draft",
            RevisionNumber = 0,
            CreatedBy = User?.Identity?.Name ?? string.Empty,
            CreatedDate = now,
            IsDeleted = false
        };

        await _context.MeetingMinutes.AddAsync(mm, ct);
        await _context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { meetingId }, mm);
    }

    [HttpPut]
    public async Task<IActionResult> Update(long meetingId, [FromBody] UpdateMeetingMinutesDto? dto, CancellationToken ct)
    {
        dto ??= new UpdateMeetingMinutesDto();
        var mm = await _context.MeetingMinutes.Where(x => x.MeetingId == meetingId && !x.IsDeleted).OrderByDescending(x => x.ModifiedDate).FirstOrDefaultAsync(ct);
        if (mm is null) return NotFound();

        mm.Summary = dto.Summary;
        mm.DiscussionNotes = dto.Notes;
        mm.PreparedByName = dto.PreparedByName;
        mm.PreparedAt = dto.PreparedAt;
        mm.Status = dto.Status ?? mm.Status;
        mm.ModifiedBy = User?.Identity?.Name ?? string.Empty;
        mm.ModifiedDate = Jarvis5.Common.Clock.UtcNowTz;

        _context.MeetingMinutes.Update(mm);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPost("/api/ea/meetings/{meetingId:long}/minutes/submit")]
    public async Task<IActionResult> Submit(long meetingId, CancellationToken ct)
    {
        var m = await _context.Meetings.FirstOrDefaultAsync(x => x.Id == meetingId && !x.IsDeleted, ct);
        if (m is null) return NotFound();

        // find latest draft or latest minutes without SubmittedAt
        var mm = await _context.MeetingMinutes.Where(x => x.MeetingId == meetingId && !x.IsDeleted && x.SubmittedAt == null).OrderByDescending(x => x.ModifiedDate).FirstOrDefaultAsync(ct);
        if (mm is null)
        {
            // nothing to submit
            return BadRequest(new { error = "No draft minutes available to submit." });
        }

        mm.SubmittedAt = Jarvis5.Common.Clock.UtcNowTz;
        mm.SubmittedByName = User?.Identity?.Name ?? string.Empty;
        mm.RevisionNumber = mm.RevisionNumber + 1;
        mm.Status = "Submitted";
        mm.ModifiedBy = User?.Identity?.Name ?? string.Empty;
        mm.ModifiedDate = Jarvis5.Common.Clock.UtcNowTz;

        _context.MeetingMinutes.Update(mm);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }
}
