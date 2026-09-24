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
    private readonly IEaTaskService _eaTaskService;

    public MeetingService(IMeetingRepository repo, EaFmsDbContext context, IMapper mapper, ICurrentUserService currentUser, IAuditService auditService, IWorkflowService workflowService, IEaTaskService eaTaskService)
    {
        _repo = repo;
        _context = context;
        _mapper = mapper;
        _currentUser = currentUser;
        _auditService = auditService;
        _workflowService = workflowService;
        _eaTaskService = eaTaskService;
    }

    public async Task<MeetingDetailResponseDto> CreateAsync(CreateMeetingRequestDto dto, CancellationToken ct = default)
    {
        var priority = NormalizePriority(dto.Priority);
        if (dto.StatusId.HasValue && !await _context.Statuses.AnyAsync(x => x.Id == dto.StatusId.Value && !x.IsDeleted, ct))
            throw new NotFoundException($"Status {dto.StatusId} not found.");
        if (dto.IntakeRequestId.HasValue && !await _context.IntakeRequests.AnyAsync(x => x.Id == dto.IntakeRequestId.Value && !x.IsDeleted, ct))
            throw new NotFoundException($"Intake request {dto.IntakeRequestId} not found.");

        var now = Clock.UtcNowTz;
        var by = _currentUser.ActorDisplay();
        var doers = dto.Doers is null ? (Array.Empty<string>(), Array.Empty<string>()) : MeetingDoers.ToArrays(dto.Doers);

        var m = new Meeting
        {
            Title = dto.Title?.Trim(),
            Description = dto.Description?.Trim(),
            Purpose = dto.Purpose?.Trim(),
            MeetingType = dto.Type,
            Category = dto.Subtype,
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
            DoerIds = doers.Item1,
            DoerNames = doers.Item2,
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
        var meetingModule = modules[0];
        var workflow = await _workflowService.GetOrCreateForBusinessRecordAsync(
            meetingModule.Id, m.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), m.IntakeRequestId, ct);
        m.WorkflowInstanceId = workflow.Id;

        // Persist the workflow link before EaTaskService re-reads the Meeting to derive
        // its exact Type/Subtype classification. EaTaskService joins this transaction.
        await _context.SaveChangesAsync(ct);
        await _eaTaskService.CreateAsync(new CreateEaTaskDto
        {
            ModuleId = meetingModule.Id,
            BusinessRecordId = m.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Task = m.Title ?? string.Empty,
            Description = m.Description,
            WorkflowInstanceId = workflow.Id
        }, ct);

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

        var taskSnapshots = await GetMeetingTaskSnapshotsAsync(meetings.Select(m => m.Id), ct);

        var workflowIds = meetings
            .Where(m => m.WorkflowInstanceId.HasValue)
            .Select(m => m.WorkflowInstanceId!.Value)
            .Distinct()
            .ToList();

        Dictionary<long, WorkAssignment> currentAssignmentsByWorkflow = new();
        Dictionary<long, WorkflowInstance> workflowsById = new();
        Dictionary<int, string> statusNamesById = new();
        // Batch-load WorkPauses for all workflows on this page in a single query.
        // Grouped in memory by WorkflowInstanceId so each meeting gets its own pauses
        // without an N+1 pattern.
        Dictionary<long, List<WorkPause>> pausesByWorkflow = new();

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

            // Single batch query: all WorkPauses for every workflow on the current page.
            var allPauses = await _context.WorkPauses.AsNoTracking()
                .Where(p => p.WorkflowInstanceId.HasValue
                    && workflowIds.Contains(p.WorkflowInstanceId!.Value)
                    && !p.IsDeleted)
                .OrderBy(p => p.StartAt)
                .ToListAsync(ct);

            pausesByWorkflow = allPauses
                .GroupBy(p => p.WorkflowInstanceId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        var now = Clock.UtcNowTz;

        foreach (var item in items)
        {
            var meeting = meetings.First(m => m.Id == item.MeetingId);
            item.Priority = meeting.Priority;
            if (taskSnapshots.TryGetValue(meeting.Id, out var task))
            {
                PopulateTaskSnapshot(item, task);
            }
            if (!meeting.WorkflowInstanceId.HasValue) continue;
            var workflowId = meeting.WorkflowInstanceId.Value;
            if (currentAssignmentsByWorkflow.TryGetValue(workflowId, out var assignment)) { item.DoerId = assignment.DoerId; item.DoerName = assignment.DoerName; }
            if (workflowsById.TryGetValue(workflowId, out var workflow))
            {
                item.StatusName = statusNamesById.TryGetValue(workflow.StatusId, out var statusName) ? statusName : null;
                item.StartedAt = workflow.TatStartedAt;
                item.CompletedAt = workflow.CompletedAt;
                item.ExecutionState = MeetingExecutionStateMapper.Map(workflow.TatStartedAt.HasValue, workflow.CompletedAt.HasValue);
                var pauses = pausesByWorkflow.TryGetValue(workflowId, out var wfPauses)
                    ? wfPauses
                    : new List<WorkPause>();
                item.IsPaused = pauses.Any(p => p.EndAt == null && WorkPauseClassifier.IsSimplePause(p));

                // TAT summary — one canonical formula (TatSummaryCalculator), shared with
                // GetByIdAsync and with Delegation's own Meeting-style TAT presentation.
                // Gated on task+AllottedTatMinutes only (not TatStartedAt), so a not-yet-started
                // meeting still gets Tat/PauseTime = zero (not null) — matching detail exactly.
                if (task is not null && task.AllottedTatMinutes.HasValue)
                {
                    item.TatSummary = TatSummaryCalculator.Calculate(
                        task.AllottedTatMinutes.Value, workflow.TatStartedAt, workflow.CompletedAt, pauses, now);

                    // Whole-minute echo of TatSummary, but only once the clock has actually
                    // started — preserving existing "unavailable" behaviour (null, not 0).
                    if (workflow.TatStartedAt.HasValue)
                    {
                        item.TatUsedMinutes = (int)item.TatSummary.Tat!.Value.TotalMinutes;
                        item.TatPausedMinutes = (int)item.TatSummary.PauseTime.TotalMinutes;
                    }
                }
                // When no task or no configured TAT exists, item.TatSummary keeps its default
                // MeetingTatSummaryDto (Tat: null, TotalTat/PauseTime: zero, PauseCount: 0) —
                // the same non-null "unavailable" shape GetByIdAsync falls back to, and
                // TatUsedMinutes/TatPausedMinutes stay null.
            }
        }

        return items;
    }

    public async Task<MeetingDetailResponseDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var m = await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Meeting {id} not found.");
        var dto = _mapper.Map<MeetingDetailResponseDto>(m);
        dto.MeetingId = m.Id;
        // MappingProfile explicitly ignores Priority on this map (it sits alongside the
        // task-snapshot fields populated below), so it must be set from the entity here —
        // otherwise the detail endpoint would always return Priority: null regardless of
        // what was stored, even though the list endpoint (QueryAsync) already surfaces it.
        dto.Priority = m.Priority;
        dto.StartedAt = null;
        var task = await GetMeetingTaskSnapshotAsync(m.Id, ct);
        if (task is not null)
        {
            PopulateTaskSnapshot(dto, task);
        }
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
                    DoerId = a.DoerId,
                    DoerName = a.DoerName,
                    AssignedById = a.AssignedById,
                    AssignedByName = a.AssignedByName,
                    AssignedAt = a.AssignedAt,
                    AssignmentType = a.AssignmentType,
                    AssignmentReason = a.Reason,
                    PreviousDoerId = prev?.DoerId,
                    PreviousDoerName = prev?.DoerName,
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
                dto.ExecutionState = MeetingExecutionStateMapper.Map(wf.TatStartedAt.HasValue, wf.CompletedAt.HasValue);

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

                // The task is the immutable allocation snapshot. Historical Meetings
                // without a task retain the existing unavailable/zero summary.
                if (task is not null)
                {
                    if (!task.AllottedTatMinutes.HasValue)
                        throw new BusinessRuleException("Meeting task does not have an allotted TAT.");
                    dto.TatSummary = TatSummaryCalculator.Calculate(
                        task.AllottedTatMinutes.Value, wf.TatStartedAt, wf.CompletedAt, pauses, Clock.UtcNowTz);
                    dto.WaitingSummary.TotalPausedMinutes = (int)dto.TatSummary.PauseTime.TotalMinutes;
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
        m.MeetingType = dto.Type;
        m.Category = dto.Subtype;
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
        m.Priority = NormalizePriority(dto.Priority);
        m.StatusId = dto.StatusId;
        m.RequiredDate = dto.RequiredDate;
        m.AgendaDueAt = dto.AgendaDueAt;
        m.MinutesDueAt = dto.MinutesDueAt;
        m.IsConfidential = dto.IsConfidential;
        if (dto.Doers is not null)
        {
            var doers = MeetingDoers.ToArrays(dto.Doers);
            m.DoerIds = doers.Ids;
            m.DoerNames = doers.Names;
        }
        m.IntakeRequestId = dto.IntakeRequestId;
        m.ModifiedBy = _currentUser.ActorDisplay();
        m.ModifiedDate = Clock.UtcNowTz;

        await _repo.UpdateAsync(m);
        _auditService.AddAudit("MEETING_UPDATE", "Meeting", nameof(Meeting), m.Id.ToString(), null, new { m.Title, m.MeetingDate }, "Meeting updated");
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var m = await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Meeting {id} not found.");
        m.IsDeleted = true;
        m.ModifiedBy = _currentUser.ActorDisplay();
        m.ModifiedDate = Clock.UtcNowTz;
        await _repo.UpdateAsync(m);
        _auditService.AddAudit("MEETING_DELETE", "Meeting", nameof(Meeting), m.Id.ToString(), null, null, "Meeting deleted");
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Priority is a frontend-owned business string (PriorityLevel is optional discovery
    /// data for a dropdown, not a persistence gate). Only whitespace is trimmed; any
    /// submitted value, including one PriorityLevel doesn't know about, is stored as-is.
    /// </summary>
    private static string? NormalizePriority(string? priority) =>
        string.IsNullOrWhiteSpace(priority) ? null : priority.Trim();

    private async Task<EaTask?> GetMeetingTaskSnapshotAsync(long meetingId, CancellationToken ct)
    {
        var tasks = await _context.Tasks.AsNoTracking()
            .Include(x => x.BusinessModule)
            .Where(x => x.BusinessRecordId == meetingId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                && !x.IsDeleted
                && !x.BusinessModule.IsDeleted
                && x.BusinessModule.Name.Trim().ToLower() == "meeting")
            .Take(2)
            .ToListAsync(ct);
        if (tasks.Count > 1)
            throw new BusinessRuleException("Meeting has ambiguous EA task snapshots.");
        return tasks.SingleOrDefault();
    }

    private async Task<Dictionary<long, EaTask>> GetMeetingTaskSnapshotsAsync(IEnumerable<long> meetingIds, CancellationToken ct)
    {
        var ids = meetingIds.Distinct().ToList();
        var recordIds = ids.Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
        var tasks = await _context.Tasks.AsNoTracking()
            .Include(x => x.BusinessModule)
            .Where(x => recordIds.Contains(x.BusinessRecordId)
                && !x.IsDeleted
                && !x.BusinessModule.IsDeleted
                && x.BusinessModule.Name.Trim().ToLower() == "meeting")
            .ToListAsync(ct);
        var result = new Dictionary<long, EaTask>();
        foreach (var group in tasks.GroupBy(x => x.BusinessRecordId))
        {
            if (group.Count() > 1)
                throw new BusinessRuleException("Meeting has ambiguous EA task snapshots.");
            if (long.TryParse(group.Key, out var meetingId))
                result[meetingId] = group.Single();
        }
        return result;
    }

    private static void PopulateTaskSnapshot(MeetingDetailResponseDto dto, EaTask task)
    {
        dto.ModuleId = task.BusinessModuleId;
        dto.ModuleName = task.BusinessModule.Name;
        dto.TatMinutes = task.AllottedTatMinutes;
        dto.Task = task.Task;
        dto.AllottedTatMinutes = task.AllottedTatMinutes;
        dto.EaTaskId = task.Id;
    }

    private static void PopulateTaskSnapshot(MeetingListItemResponseDto dto, EaTask task)
    {
        dto.ModuleId = task.BusinessModuleId;
        dto.ModuleName = task.BusinessModule.Name;
        dto.TatMinutes = task.AllottedTatMinutes;
    }

    internal static TimeSpan GetPausedDuration(DateTime tatStart, DateTime end, IEnumerable<WorkPause> pauses)
        => WorkPauseClassifier.GetPausedDuration(tatStart, end, pauses);


}
