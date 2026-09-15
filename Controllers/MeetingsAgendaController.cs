using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/meetings/{meetingId:long}/agenda")]
public class MeetingsAgendaController : ControllerBase
{
    private readonly EaFmsDbContext _context;

    public MeetingsAgendaController(EaFmsDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get(long meetingId, CancellationToken ct)
    {
        var m = await _context.Meetings.FirstOrDefaultAsync(x => x.Id == meetingId && !x.IsDeleted, ct);
        if (m is null) return NotFound();

        var list = await _context.MeetingAgendas.Where(a => a.MeetingId == meetingId && !a.IsDeleted).OrderBy(a => a.SequenceNumber).ToListAsync(ct);
        return Ok(list.Select(a => new MeetingAgendaDto {
            Id = a.Id,
            OrderNo = a.SequenceNumber,
            Title = a.Title,
            Description = a.Description,
            OwnerName = a.OwnerName,
            IsReady = a.IsPrepared,
            DueAt = a.RequiredBy,
            PreparedAt = a.PreparedAt
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create(long meetingId, [FromBody] CreateMeetingAgendaDto? dto, CancellationToken ct)
    {
        dto ??= new CreateMeetingAgendaDto();
        var m = await _context.Meetings.FirstOrDefaultAsync(x => x.Id == meetingId && !x.IsDeleted, ct);
        if (m is null) return NotFound();

        var now = Jarvis5.Common.Clock.UtcNowTz;
        var agenda = new MeetingAgenda
        {
            MeetingId = meetingId,
            SequenceNumber = dto.OrderNo,
            Title = dto.Title,
            Description = dto.Description,
            OwnerName = dto.OwnerName,
            IsPrepared = dto.IsReady,
            RequiredBy = dto.DueAt,
            CreatedBy = User?.Identity?.Name ?? string.Empty,
            CreatedDate = now,
            IsDeleted = false
        };

        await _context.MeetingAgendas.AddAsync(agenda, ct);
        await _context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { meetingId }, agenda);
    }

    [HttpPut("{agendaId:long}")]
    public async Task<IActionResult> Update(long meetingId, long agendaId, [FromBody] UpdateMeetingAgendaDto? dto, CancellationToken ct)
    {
        dto ??= new UpdateMeetingAgendaDto();
        var agenda = await _context.MeetingAgendas.FirstOrDefaultAsync(a => a.Id == agendaId && a.MeetingId == meetingId && !a.IsDeleted, ct);
        if (agenda is null) return NotFound();

        agenda.SequenceNumber = dto.OrderNo;
        agenda.Title = dto.Title;
        agenda.Description = dto.Description;
        agenda.OwnerName = dto.OwnerName;
        agenda.IsPrepared = dto.IsReady;
        agenda.RequiredBy = dto.DueAt;
        agenda.PreparedAt = dto.PreparedAt;
        agenda.ModifiedBy = User?.Identity?.Name ?? string.Empty;
        agenda.ModifiedDate = Jarvis5.Common.Clock.UtcNowTz;

        _context.MeetingAgendas.Update(agenda);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpDelete("{agendaId:long}")]
    public async Task<IActionResult> Delete(long meetingId, long agendaId, CancellationToken ct)
    {
        var agenda = await _context.MeetingAgendas.FirstOrDefaultAsync(a => a.Id == agendaId && a.MeetingId == meetingId && !a.IsDeleted, ct);
        if (agenda is null) return NotFound();

        agenda.IsDeleted = true;
        agenda.ModifiedBy = User?.Identity?.Name ?? string.Empty;
        agenda.ModifiedDate = Jarvis5.Common.Clock.UtcNowTz;
        _context.MeetingAgendas.Update(agenda);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }
}
