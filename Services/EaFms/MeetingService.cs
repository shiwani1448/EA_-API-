using AutoMapper;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public class MeetingService : IMeetingService
{
    private readonly IMeetingRepository _repo;
    private readonly EaFmsDbContext _context;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;
    private readonly IWorkflowService _workflowService;

    public MeetingService(IMeetingRepository repo, EaFmsDbContext context, IMapper mapper, ICurrentUserService currentUser, IAuditService auditService, IWorkflowService workflowService)
    {
        _repo = repo;
        _context = context;
        _mapper = mapper;
        _currentUser = currentUser;
        _auditService = auditService;
        _workflowService = workflowService;
    }

    public async Task<MeetingDetailResponseDto> CreateAsync(CreateMeetingRequestDto dto, CancellationToken ct = default)
    {
        var priority = await ResolvePriorityAsync(dto.Priority, ct);
        if (dto.StatusId.HasValue && !await _context.Statuses.AnyAsync(x => x.Id == dto.StatusId.Value && !x.IsDeleted, ct))
            throw new NotFoundException($"Status {dto.StatusId} not found.");
        if (dto.IntakeRequestId.HasValue && !await _context.IntakeRequests.AnyAsync(x => x.Id == dto.IntakeRequestId.Value && !x.IsDeleted, ct))
            throw new NotFoundException($"Intake request {dto.IntakeRequestId} not found.");

        var now = Clock.UtcNowTz;
        var by = _currentUser.UserName ?? _currentUser.UserId.ToString();

        var m = new Meeting
        {
            Title = dto.Title?.Trim(),
            Description = dto.Description?.Trim(),
            Purpose = dto.Purpose?.Trim(),
            MeetingType = dto.MeetingType,
            Category = dto.Category,
            Source = dto.Source,
            SourceChannel = dto.SourceChannel,
            SourceReferenceId = dto.SourceReferenceId,
            MeetingDate = dto.MeetingDate,
            StartDateTime = dto.StartDateTime,
            EndDateTime = dto.EndDateTime,
            Location = dto.Location,
            MeetingMode = dto.MeetingMode,
            MeetingLink = dto.MeetingLink,
            OrganizerId = dto.OrganizerId,
            OrganizerName = dto.OrganizerName,
            Priority = priority,
            StatusId = dto.StatusId,
            RequiredDate = dto.RequiredDate,
            AgendaDueAt = dto.AgendaDueAt,
            MinutesDueAt = dto.MinutesDueAt,
            IsConfidential = dto.IsConfidential,
            IntakeRequestId = dto.IntakeRequestId,
            CreatedBy = by,
            CreatedDate = now,
            IsDeleted = false
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        await _repo.AddAsync(m, ct);
        await _context.SaveChangesAsync(ct);

        if (string.IsNullOrWhiteSpace(m.MeetingNumber))
            m.MeetingNumber = $"MTG-{m.Id:D6}";

        var modules = await _context.BusinessModules
            .Where(x => x.IsActive && !x.IsDeleted && x.Name.Trim().ToLower() == "meeting")
            .ToListAsync(ct);
        if (modules.Count != 1)
            throw new InvalidOperationException("Exactly one active Meeting business-module catalog entry is required.");
        var workflow = await _workflowService.GetOrCreateForBusinessRecordAsync(
            modules[0].Id, m.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), m.IntakeRequestId, ct);
        m.WorkflowInstanceId = workflow.Id;

        _auditService.AddAudit("MEETING_CREATE", "Meeting", nameof(Meeting), m.Id.ToString(), null, new { m.Title, m.MeetingDate }, "Meeting created");
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await GetByIdAsync(m.Id, ct);
    }

    public async Task<List<MeetingListItemResponseDto>> QueryAsync(string? search = null, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var meetings = await _repo.QueryAsync(q =>
            q.OrderByDescending(m => m.StartDateTime)
            .Skip((page - 1) * pageSize).Take(pageSize), ct);

        var items = meetings.Select(m => _mapper.Map<MeetingListItemResponseDto>(m)).ToList();
        if (items.Count == 0) return items;

        var workflowIds = meetings
            .Where(m => m.WorkflowInstanceId.HasValue)
            .Select(m => m.WorkflowInstanceId!.Value)
            .Distinct()
            .ToList();

        Dictionary<long, WorkAssignment> currentAssignmentsByWorkflow = new();
        Dictionary<long, WorkflowInstance> workflowsById = new();
        Dictionary<int, string> statusNamesById = new();

        if (workflowIds.Count > 0)
        {
            var assignments = await _context.WorkAssignments.AsNoTracking()
                .Where(a => a.WorkflowInstanceId.HasValue
                    && workflowIds.Contains(a.WorkflowInstanceId.Value)
                    && a.IsCurrent
                    && !a.IsDeleted)
                .ToListAsync(ct);

            currentAssignmentsByWorkflow = assignments
                .GroupBy(a => a.WorkflowInstanceId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(a => a.AssignedAt).First());

            var workflows = await _context.WorkflowInstances.AsNoTracking()
                .Where(w => workflowIds.Contains(w.Id) && !w.IsDeleted)
                .ToListAsync(ct);

            workflowsById = workflows.ToDictionary(w => w.Id);

            var statusIds = workflows.Select(w => w.StatusId).Distinct().ToList();
            if (statusIds.Count > 0)
            {
                statusNamesById = await _context.Statuses.AsNoTracking()
                    .Where(s => statusIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
            }
        }

        foreach (var item in items)
        {
            var meeting = meetings.First(m => m.Id == item.MeetingId);
            item.Priority = meeting.Priority;
            if (!meeting.WorkflowInstanceId.HasValue) continue;
            var workflowId = meeting.WorkflowInstanceId.Value;
            if (currentAssignmentsByWorkflow.TryGetValue(workflowId, out var assignment)) { item.AssignedToId = assignment.AssignedToId; item.AssignedToName = assignment.AssignedToName; }
            if (workflowsById.TryGetValue(workflowId, out var workflow))
            {
                item.StatusName = statusNamesById.TryGetValue(workflow.StatusId, out var statusName) ? statusName : null;
                item.StartedAt = workflow.TatStartedAt;
                item.CompletedAt = workflow.CompletedAt;
                item.ExecutionState = string.Equals(item.StatusName, "Completed", StringComparison.OrdinalIgnoreCase) ? "Completed" : "Captured";
            }
        }

        return items;
    }

    public async Task<MeetingDetailResponseDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var m = await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Meeting {id} not found.");
        var dto = _mapper.Map<MeetingDetailResponseDto>(m);
        dto.MeetingId = m.Id;
        dto.StartedAt = null;
        // Populate assignment summary
        if (m.WorkflowInstanceId.HasValue)
        {
            var a = await _context.WorkAssignments
                .Where(w => w.WorkflowInstanceId == m.WorkflowInstanceId && w.IsCurrent && !w.IsDeleted)
                .OrderByDescending(w => w.AssignedAt)
                .FirstOrDefaultAsync(ct);

            if (a != null)
            {
                var prev = await _context.WorkAssignments
                    .Where(w => w.WorkflowInstanceId == m.WorkflowInstanceId && !w.IsDeleted && w.IsCurrent == false)
                    .OrderByDescending(w => w.AssignedAt)
                    .FirstOrDefaultAsync(ct);

                dto.AssignmentSummary = new MeetingAssignmentSummaryDto
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
            }

            // workflow summary
            var wf = await _context.WorkflowInstances.FirstOrDefaultAsync(w => w.Id == m.WorkflowInstanceId.Value && !w.IsDeleted, ct);
            if (wf != null)
            {
                var status = await _context.Statuses.FirstOrDefaultAsync(s => s.Id == wf.StatusId, ct);
                dto.StatusName = status?.Name;
                dto.StartedAt = wf.TatStartedAt;
                dto.CompletedAt = wf.CompletedAt;
                // waiting/pause summary reuse
                var pauses = await _context.WorkPauses
                    .Where(p => p.WorkflowInstanceId == wf.Id && !p.IsDeleted)
                    .OrderBy(p => p.StartAt)
                    .ToListAsync(ct);

                var openSimplePause = pauses.FirstOrDefault(p => p.EndAt == null && WorkPauseClassifier.IsSimplePause(p));
                dto.IsPaused = openSimplePause is not null;
                dto.ExecutionState = string.Equals(status?.Name, "Completed", StringComparison.OrdinalIgnoreCase) ? "Completed" : openSimplePause is not null ? "Paused" : string.Equals(status?.Name, "In Progress", StringComparison.OrdinalIgnoreCase) ? "Running" : "Captured";

                dto.WaitingSummary = new MeetingWaitingSummaryDto
                {
                    IsPaused = pauses.Any(p => p.EndAt == null),
                    CurrentPauseId = pauses.FirstOrDefault(p => p.EndAt == null)?.Id,
                    PauseStartedAt = pauses.FirstOrDefault(p => p.EndAt == null)?.StartAt,
                    PauseReason = pauses.FirstOrDefault(p => p.EndAt == null)?.Reason,
                    WaitingOnId = pauses.FirstOrDefault(p => p.EndAt == null)?.WaitingOnId,
                    WaitingOnName = pauses.FirstOrDefault(p => p.EndAt == null)?.WaitingOnName,
                    WaitingOnExternal = pauses.FirstOrDefault(p => p.EndAt == null)?.WaitingOnExternal,
                    ResponseOwnerId = pauses.FirstOrDefault(p => p.EndAt == null)?.ResponseOwnerId,
                    ResponseOwnerName = pauses.FirstOrDefault(p => p.EndAt == null)?.ResponseOwnerName,
                    ExpectedResponseAt = pauses.FirstOrDefault(p => p.EndAt == null)?.ExpectedResponseAt,
                    PauseCount = pauses.Count
                };

                // total paused minutes overlapping TAT
                if (wf.TatStartedAt.HasValue)
                {
                    var end = (wf.CompletedAt ?? DateTime.UtcNow);
                    var tatStart = wf.TatStartedAt.Value;
                    int totalPaused = 0;
                    foreach (var p in pauses.Where(WorkPauseClassifier.IsSimplePause))
                    {
                        var pauseStart = p.StartAt;
                        var pauseEnd = p.EndAt ?? DateTime.UtcNow;
                        var overlapStart = pauseStart > tatStart ? pauseStart : tatStart;
                        var overlapEnd = pauseEnd < end ? pauseEnd : end;
                        if (overlapEnd > overlapStart)
                        {
                            totalPaused += (int)(overlapEnd - overlapStart).TotalMinutes;
                        }
                    }

                    dto.WaitingSummary.TotalPausedMinutes = totalPaused;

                    // TAT summary
                    // TatUsedMinutes = total minutes from TatStartedAt to now/completion minus paused minutes
                    var used = (int)((end - tatStart).TotalMinutes) - totalPaused;
                    dto.TatSummary = new MeetingTatSummaryDto
                    {
                        Tat = null,
                        TotalTat = TimeSpan.FromMinutes(used < 0 ? 0 : used),
                        TatDifference = TimeSpan.Zero,
                        StartTime = wf.TatStartedAt,
                        EndTime = wf.CompletedAt,
                        LastActiveTime = wf.CompletedAt ?? (pauses.Where(p => p.EndAt.HasValue).OrderByDescending(p => p.EndAt).Select(p => p.EndAt).FirstOrDefault() ?? wf.TatStartedAt),
                        PauseTime = TimeSpan.FromMinutes(totalPaused),
                        PauseCount = pauses.Count(WorkPauseClassifier.IsSimplePause)
                    };
                }
            }
        }

        dto.TatSummary ??= new MeetingTatSummaryDto { Tat = null, TotalTat = TimeSpan.Zero, TatDifference = TimeSpan.Zero, PauseTime = TimeSpan.Zero, PauseCount = 0 };

        // followups summary
        var followups = await _context.Followups
            .Where(f => _context.BusinessModules.Any(module => module.Id == f.BusinessModuleId && !module.IsDeleted && module.Name.Trim().ToLower() == "meeting") && f.BusinessRecordId == m.Id.ToString() && !f.IsDeleted)
            .ToListAsync(ct);

        if (followups.Any())
        {
            dto.FollowupSummary = new MeetingFollowupSummaryDto
            {
                TotalFollowups = followups.Count,
                OpenFollowups = followups.Count(f => f.CompletedAt == null),
                CompletedFollowups = followups.Count(f => f.CompletedAt != null),
                OverdueFollowups = followups.Count(f => f.CompletedAt == null && f.DueAt != default && f.DueAt < Clock.UtcNowTz),
                LastFollowupAt = followups.Max(f => f.LastFollowupAt),
                NextFollowupAt = followups.Min(f => f.NextFollowupAt),
                WaitingOnId = followups.FirstOrDefault(f => f.WaitingOnId != null)?.WaitingOnId,
                WaitingOnName = followups.FirstOrDefault(f => f.WaitingOnName != null)?.WaitingOnName,
                WaitingOnExternal = followups.FirstOrDefault(f => f.WaitingOnExternal != null)?.WaitingOnExternal,
                ResponseOwnerId = followups.FirstOrDefault(f => f.ResponseOwnerId != null)?.ResponseOwnerId,
                ResponseOwnerName = followups.FirstOrDefault(f => f.ResponseOwnerName != null)?.ResponseOwnerName,
                ExpectedResponseAt = followups.FirstOrDefault(f => f.ExpectedResponseAt != null)?.ExpectedResponseAt,
                HasOverdueFollowup = followups.Any(f => f.CompletedAt == null && f.DueAt != default && f.DueAt < Clock.UtcNowTz)
            };
        }

        return dto;
    }

    public async Task UpdateAsync(long id, UpdateMeetingRequestDto dto, CancellationToken ct = default)
    {
        var m = await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Meeting {id} not found.");
        m.Title = dto.Title?.Trim();
        m.Description = dto.Description?.Trim();
        m.Purpose = dto.Purpose?.Trim();
        m.MeetingType = dto.MeetingType;
        m.Category = dto.Category;
        m.Source = dto.Source;
        m.SourceChannel = dto.SourceChannel;
        m.SourceReferenceId = dto.SourceReferenceId;
        m.MeetingDate = dto.MeetingDate;
        m.StartDateTime = dto.StartDateTime;
        m.EndDateTime = dto.EndDateTime;
        m.Location = dto.Location;
        m.MeetingMode = dto.MeetingMode;
        m.MeetingLink = dto.MeetingLink;
        m.OrganizerId = dto.OrganizerId;
        m.OrganizerName = dto.OrganizerName;
        m.Priority = await ResolvePriorityAsync(dto.Priority, ct);
        m.StatusId = dto.StatusId;
        m.RequiredDate = dto.RequiredDate;
        m.AgendaDueAt = dto.AgendaDueAt;
        m.MinutesDueAt = dto.MinutesDueAt;
        m.IsConfidential = dto.IsConfidential;
        m.IntakeRequestId = dto.IntakeRequestId;
        m.ModifiedBy = _currentUser.UserName ?? _currentUser.UserId.ToString();
        m.ModifiedDate = Clock.UtcNowTz;

        await _repo.UpdateAsync(m);
        _auditService.AddAudit("MEETING_UPDATE", "Meeting", nameof(Meeting), m.Id.ToString(), null, new { m.Title, m.MeetingDate }, "Meeting updated");
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var m = await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Meeting {id} not found.");
        m.IsDeleted = true;
        m.ModifiedBy = _currentUser.UserName ?? _currentUser.UserId.ToString();
        m.ModifiedDate = Clock.UtcNowTz;
        await _repo.UpdateAsync(m);
        _auditService.AddAudit("MEETING_DELETE", "Meeting", nameof(Meeting), m.Id.ToString(), null, null, "Meeting deleted");
        await _context.SaveChangesAsync(ct);
    }

    private async Task<string?> ResolvePriorityAsync(string? priority, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(priority)) return null;
        var normalized = priority.Trim();
        return await _context.PriorityLevels.AsNoTracking().Where(p => p.IsActive && !p.IsDeleted && p.Name.ToLower() == normalized.ToLower()).Select(p => p.Name).SingleOrDefaultAsync(ct)
            ?? throw new BadRequestException($"Priority '{normalized}' is not an active priority level.");
    }


}
