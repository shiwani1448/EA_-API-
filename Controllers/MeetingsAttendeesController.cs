using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/meetings/{meetingId:long}/attendees")]
public class MeetingsAttendeesController : ControllerBase
{
    private readonly EaFmsDbContext _context;

    public MeetingsAttendeesController(EaFmsDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get(long meetingId, CancellationToken ct)
    {
        var m = await _context.Meetings.FirstOrDefaultAsync(x => x.Id == meetingId && !x.IsDeleted, ct);
        if (m is null) return NotFound();

        var list = await _context.MeetingAttendees.Where(a => a.MeetingId == meetingId && !a.IsDeleted).ToListAsync(ct);
        return Ok(list.Select(a => new MeetingAttendeeDto {
            Id = a.Id,
            Name = a.ParticipantName,
            Email = a.Email,
            Role = a.Role,
            InviteStatus = a.InvitationStatus,
            AttendanceStatus = a.AttendanceStatus,
            ConfirmedAt = a.ConfirmedAt,
            AttendedAt = a.AttendedAt,
            Remarks = a.Remarks
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create(long meetingId, [FromBody] CreateMeetingAttendeeDto? dto, CancellationToken ct)
    {
        dto ??= new CreateMeetingAttendeeDto();
        var m = await _context.Meetings.FirstOrDefaultAsync(x => x.Id == meetingId && !x.IsDeleted, ct);
        if (m is null) return NotFound();

        var now = Jarvis5.Common.Clock.UtcNowTz;
        var a = new MeetingAttendee
        {
            MeetingId = meetingId,
            ParticipantName = dto.Name,
            Email = dto.Email,
            Role = dto.Role,
            InvitationStatus = dto.InviteStatus,
            AttendanceStatus = dto.AttendanceStatus,
            Remarks = dto.Remarks,
            CreatedBy = User?.Identity?.Name ?? string.Empty,
            CreatedDate = now,
            IsDeleted = false
        };

        await _context.MeetingAttendees.AddAsync(a, ct);
        await _context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { meetingId }, a);
    }

    [HttpPut("{attendeeId:long}")]
    public async Task<IActionResult> Update(long meetingId, long attendeeId, [FromBody] UpdateMeetingAttendeeDto? dto, CancellationToken ct)
    {
        dto ??= new UpdateMeetingAttendeeDto();
        var a = await _context.MeetingAttendees.FirstOrDefaultAsync(x => x.Id == attendeeId && x.MeetingId == meetingId && !x.IsDeleted, ct);
        if (a is null) return NotFound();

        a.ParticipantName = dto.Name;
        a.Email = dto.Email;
        a.Role = dto.Role;
        a.InvitationStatus = dto.InviteStatus;
        a.AttendanceStatus = dto.AttendanceStatus;
        a.ConfirmedAt = dto.ConfirmedAt;
        a.AttendedAt = dto.AttendedAt;
        a.Remarks = dto.Remarks;
        a.ModifiedBy = User?.Identity?.Name ?? string.Empty;
        a.ModifiedDate = Jarvis5.Common.Clock.UtcNowTz;

        _context.MeetingAttendees.Update(a);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpDelete("{attendeeId:long}")]
    public async Task<IActionResult> Delete(long meetingId, long attendeeId, CancellationToken ct)
    {
        var a = await _context.MeetingAttendees.FirstOrDefaultAsync(x => x.Id == attendeeId && x.MeetingId == meetingId && !x.IsDeleted, ct);
        if (a is null) return NotFound();

        a.IsDeleted = true;
        a.ModifiedBy = User?.Identity?.Name ?? string.Empty;
        a.ModifiedDate = Jarvis5.Common.Clock.UtcNowTz;
        _context.MeetingAttendees.Update(a);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }
}
