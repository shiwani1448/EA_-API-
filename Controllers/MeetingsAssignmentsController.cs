using AutoMapper;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.EaFms;
using Jarvis5.Services;
using Jarvis5.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/meetings/{meetingId:long}/assignment")]
public class MeetingsAssignmentsController : ControllerBase
{
    private readonly EaFmsDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;
    private readonly IMapper _mapper;

    public MeetingsAssignmentsController(EaFmsDbContext context, ICurrentUserService currentUser, IAuditService auditService, IMapper mapper)
    {
        _context = context;
        _currentUser = currentUser;
        _auditService = auditService;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<IActionResult> GetCurrent(long meetingId, CancellationToken ct)
    {
        var meeting = await _context.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId && !m.IsDeleted, ct);
        if (meeting is null) return NotFound();

        if (!meeting.WorkflowInstanceId.HasValue) return NotFound(new { error = "Meeting has no workflow instance" });

        var a = await _context.WorkAssignments
            .Where(w => w.WorkflowInstanceId == meeting.WorkflowInstanceId && w.IsCurrent && !w.IsDeleted)
            .OrderByDescending(w => w.AssignedAt)
            .FirstOrDefaultAsync(ct);

        if (a is null) return Ok(new MeetingAssignmentSummaryDto { IsAssigned = false });

        var prev = await _context.WorkAssignments
            .Where(w => w.WorkflowInstanceId == meeting.WorkflowInstanceId && !w.IsDeleted && w.IsCurrent == false)
            .OrderByDescending(w => w.AssignedAt)
            .FirstOrDefaultAsync(ct);

        var dto = new MeetingAssignmentSummaryDto
        {
            AssignedToId = a.AssignedToId,
            AssignedToName = a.AssignedToName,
            AssignedById = a.AssignedById,
            AssignedByName = a.AssignedByName,
            AssignedAt = a.AssignedAt,
            AssignmentType = a.AssignmentType,
            AssignmentReason = a.Reason,
            PreviousAssignedToId = prev?.AssignedToId,
            PreviousAssignedToName = prev?.AssignedToName,
            IsAssigned = true,
            IsCurrent = a.IsCurrent
        };

        return Ok(dto);
    }

    [HttpGet("/api/ea/meetings/{meetingId:long}/assignment-history")]
    public async Task<IActionResult> GetHistory(long meetingId, CancellationToken ct)
    {
        var meeting = await _context.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId && !m.IsDeleted, ct);
        if (meeting is null) return NotFound();
        if (!meeting.WorkflowInstanceId.HasValue) return Ok(new List<MeetingAssignmentSummaryDto>());

        var list = await _context.WorkAssignments
            .Where(w => w.WorkflowInstanceId == meeting.WorkflowInstanceId && !w.IsDeleted)
            .OrderByDescending(w => w.AssignedAt)
            .ToListAsync(ct);

        var dtos = list.Select(w => new MeetingAssignmentSummaryDto
        {
            AssignedToId = w.AssignedToId,
            AssignedToName = w.AssignedToName,
            AssignedById = w.AssignedById,
            AssignedByName = w.AssignedByName,
            AssignedAt = w.AssignedAt,
            AssignmentType = w.AssignmentType,
            AssignmentReason = w.Reason,
            IsAssigned = true,
            IsCurrent = w.IsCurrent
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost("/api/ea/meetings/{meetingId:long}/assign")]
    public async Task<IActionResult> Assign(long meetingId, [FromBody] CreateAssignmentRequestDto? dto, CancellationToken ct)
    {
        dto ??= new CreateAssignmentRequestDto();
        var meeting = await _context.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId && !m.IsDeleted, ct);
        if (meeting is null) return NotFound();
        if (!meeting.WorkflowInstanceId.HasValue) return BadRequest(new { error = "Meeting has no workflow instance" });

        // reuse existing assignment engine logic: close existing and create new
        var workflowId = meeting.WorkflowInstanceId.Value;
        var existing = await _context.WorkAssignments.FirstOrDefaultAsync(a => a.WorkflowInstanceId == workflowId && a.IsCurrent && !a.IsDeleted, ct);

        var now = Clock.UtcNowTz;
        var byId = _currentUser.UserId.ToString();
        var byName = _currentUser.UserName;

        using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            if (existing != null)
            {
                existing.IsCurrent = false;
                existing.UnassignedAt = now;
                existing.UnassignedById = byId;
                existing.UnassignedByName = byName;
                existing.ModifiedBy = byName ?? byId;
                existing.ModifiedDate = now;
                _context.WorkAssignments.Update(existing);
                _auditService.AddAudit("MEETING_REASSIGN", "Meeting", nameof(WorkAssignment), existing.Id.ToString(), null, new { existing.WorkflowInstanceId, existing.AssignedToId }, "Assignment closed");
            }

            var assignment = new WorkAssignment
            {
                WorkflowInstanceId = workflowId,
                AssignedToId = dto.AssignedToId ?? string.Empty,
                AssignedToName = dto.AssignedToName,
                AssignedById = byId,
                AssignedByName = byName,
                AssignedAt = now,
                IsCurrent = true,
                IsDeleted = false,
                Reason = dto.Reason,
                AssignmentType = dto.AssignmentType,
                CreatedBy = byName ?? byId,
                CreatedDate = now
            };

            await _context.WorkAssignments.AddAsync(assignment, ct);

            // sync workflow
            var wf = await _context.WorkflowInstances.FindAsync(new object[] { workflowId }, ct);
            if (wf != null)
            {
                wf.AssignedToId = assignment.AssignedToId;
                wf.AssignedToName = assignment.AssignedToName;
                wf.ModifiedBy = byName ?? byId;
                wf.ModifiedDate = now;
                _context.WorkflowInstances.Update(wf);
            }

            _auditService.AddAudit("MEETING_ASSIGN", "Meeting", nameof(WorkAssignment), assignment.Id.ToString(), null, new { assignment.WorkflowInstanceId, assignment.AssignedToId }, "Assignment created");

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var resp = new MeetingAssignmentSummaryDto
            {
                AssignedToId = assignment.AssignedToId,
                AssignedToName = assignment.AssignedToName,
                AssignedById = assignment.AssignedById,
                AssignedByName = assignment.AssignedByName,
                AssignedAt = assignment.AssignedAt,
                AssignmentType = assignment.AssignmentType,
                AssignmentReason = assignment.Reason,
                IsAssigned = true,
                IsCurrent = true
            };

            return CreatedAtAction(nameof(GetCurrent), new { meetingId }, resp);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    [HttpPost("/api/ea/meetings/{meetingId:long}/reassign")]
    public Task<IActionResult> Reassign(long meetingId, [FromBody] CreateAssignmentRequestDto? dto, CancellationToken ct) => Assign(meetingId, dto, ct);
}
