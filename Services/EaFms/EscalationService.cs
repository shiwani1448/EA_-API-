using AutoMapper;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public class EscalationService : IEscalationService
{
    private readonly IEscalationRepository _repo;
    private readonly EaFmsDbContext _context;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;

    public EscalationService(IEscalationRepository repo, EaFmsDbContext context, IMapper mapper, ICurrentUserService currentUser, IAuditService auditService)
    {
        _repo = repo;
        _context = context;
        _mapper = mapper;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<EscalationResponseDto> CreateAsync(CreateEscalationRequestDto dto, CancellationToken ct = default)
    {
        var followup = await _context.Followups.FirstOrDefaultAsync(f => f.Id == dto.FollowupId && !f.IsDeleted, ct);
        if (followup is null) throw new Jarvis5.Common.NotFoundException($"Followup {dto.FollowupId} not found.");

        var level = await _context.EscalationLevels.FirstOrDefaultAsync(l => l.Id == dto.EscalationLevelId && !l.IsDeleted, ct);
        if (level is null) throw new Jarvis5.Common.NotFoundException($"EscalationLevel {dto.EscalationLevelId} not found.");

        if (dto.NextEscalationLevelId.HasValue)
        {
            var nx = await _context.EscalationLevels.FirstOrDefaultAsync(l => l.Id == dto.NextEscalationLevelId.Value && !l.IsDeleted, ct);
            if (nx is null) throw new Jarvis5.Common.NotFoundException($"NextEscalationLevel {dto.NextEscalationLevelId.Value} not found.");
            // enforce ordering: next level must be greater than current
            if (nx.Level <= level.Level) throw new Jarvis5.Common.BadRequestException("NextEscalationLevel must be greater than current EscalationLevel.");
        }

        var now = Clock.UtcNowTz;
        var by = _currentUser.ActorDisplay();

        var esc = new Escalation
        {
            FollowupId = dto.FollowupId,
            // derive linkage from followup when present
            WorkflowInstanceId = followup.WorkflowInstanceId,
            IntakeRequestId = followup.IntakeRequestId,
            BusinessModuleId = followup.BusinessModuleId,
            BusinessRecordId = followup.BusinessRecordId,
            EscalationLevelId = dto.EscalationLevelId,
            InitiatedAt = now,
            ResolvedAt = null,
            Notes = dto.Notes?.Trim(),
            EscalatedToId = dto.EscalatedToId,
            EscalatedToName = dto.EscalatedToName,
            NextEscalationAt = dto.NextEscalationAt,
            NextEscalationLevelId = dto.NextEscalationLevelId,
            IsDeleted = false,
            CreatedBy = by,
            CreatedDate = now
        };

        await _repo.AddAsync(esc, ct);

        _auditService.AddAudit(
            actionType: "ESCALATION_CREATE",
            module: "Escalation",
            entityName: nameof(Escalation),
            entityId: esc.Id.ToString(),
            oldValues: null,
            newValues: new { esc.Id, esc.FollowupId, esc.WorkflowInstanceId, esc.IntakeRequestId, esc.BusinessModuleId, esc.BusinessRecordId, esc.EscalationLevelId, esc.EscalatedToId, esc.NextEscalationAt, esc.NextEscalationLevelId },
            description: "Escalation created");

        await _context.SaveChangesAsync(ct);

        var response = _mapper.Map<EscalationResponseDto>(esc);
        response.EscalationLevelName = level.Name;
        return response;
    }

    public async Task<EscalationResponseDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var e = await _repo.GetByIdAsync(id, ct) ?? throw new Jarvis5.Common.NotFoundException($"Escalation {id} not found.");
        var dto = _mapper.Map<EscalationResponseDto>(e);
        var level = await _context.EscalationLevels.FindAsync(new object[] { e.EscalationLevelId }, ct);
        dto.EscalationLevelName = level?.Name;
        dto.EscalationLevelNumber = level?.Level;
        // derive EscalationState
        dto.EscalationState = e.ResolvedAt != null ? "Resolved" : (e.AcknowledgedAt != null ? "Acknowledged" : "Open");
        // populate acknowledgement/resolution actor fields explicitly
        dto.AcknowledgedById = e.AcknowledgedById;
        dto.AcknowledgedByName = e.AcknowledgedByName;
        dto.ResolvedById = e.ResolvedById;
        dto.ResolvedByName = e.ResolvedByName;
        dto.AcknowledgementNote = e.AcknowledgementNote;
        dto.ResolutionNote = e.ResolutionNote;
        return dto;
    }

    public async Task<List<EscalationResponseDto>> GetByFollowupIdAsync(long followupId, CancellationToken ct = default)
    {
        var list = await _repo.GetByFollowupIdAsync(followupId, ct);
        var dtos = new List<EscalationResponseDto>();
        foreach (var e in list)
        {
            var dto = _mapper.Map<EscalationResponseDto>(e);
            var level = await _context.EscalationLevels.FindAsync(new object[] { e.EscalationLevelId }, ct);
            dto.EscalationLevelName = level?.Name;
            dto.EscalationLevelNumber = level?.Level;
            dto.EscalationState = e.ResolvedAt != null ? "Resolved" : (e.AcknowledgedAt != null ? "Acknowledged" : "Open");
            dto.AcknowledgedById = e.AcknowledgedById;
            dto.AcknowledgedByName = e.AcknowledgedByName;
            dto.AcknowledgementNote = e.AcknowledgementNote;
            dto.ResolvedById = e.ResolvedById;
            dto.ResolvedByName = e.ResolvedByName;
            dto.ResolutionNote = e.ResolutionNote;
            dtos.Add(dto);
        }

        return dtos;
    }

    public async Task<PagedResult<EscalationResponseDto>> GetPagedAsync(EscalationListQueryDto query, CancellationToken ct = default)
    {
        if (query.Page < 1 || query.PageSize < 1) throw new BadRequestException("Page and PageSize must be positive.");
        query.PageSize = Math.Min(query.PageSize, 100);
        if (((long)query.Page - 1) * query.PageSize > int.MaxValue) throw new BadRequestException("Page offset is too large.");

        var escalations = _context.Escalations.AsNoTracking().Where(e => !e.IsDeleted);
        var total = await escalations.CountAsync(ct);
        var page = await escalations.OrderByDescending(e => e.InitiatedAt).ThenByDescending(e => e.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new PagedResult<EscalationResponseDto>
        {
            Items = await EnrichGeneralListAsync(page, ct),
            PageNumber = query.Page,
            PageSize = query.PageSize,
            TotalCount = total
        };
    }

    private sealed record EscalationTaskContext(long Id, long BusinessModuleId, string BusinessRecordId, string Task);

    private async Task<List<EscalationResponseDto>> EnrichGeneralListAsync(IReadOnlyList<Escalation> escalations, CancellationToken ct)
    {
        var dtos = escalations.Select(e => _mapper.Map<EscalationResponseDto>(e)).ToList();
        if (escalations.Count == 0) return dtos;

        var levelIds = escalations.Select(e => e.EscalationLevelId).Distinct().ToList();
        var levels = await _context.EscalationLevels.AsNoTracking().Where(l => !l.IsDeleted && levelIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, ct);
        var followupIds = escalations.Where(e => e.FollowupId.HasValue).Select(e => e.FollowupId!.Value).Distinct().ToList();
        var followups = await _context.Followups.AsNoTracking().Where(f => !f.IsDeleted && followupIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, ct);
        var moduleIds = followups.Values.Where(f => f.BusinessModuleId.HasValue).Select(f => f.BusinessModuleId!.Value).Distinct().ToList();
        var modules = moduleIds.Count == 0 ? new Dictionary<long, string>()
            : await _context.BusinessModules.AsNoTracking().Where(m => !m.IsDeleted && moduleIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => m.Name, ct);
        var recordIds = followups.Values.Where(f => f.BusinessRecordId != null).Select(f => f.BusinessRecordId!).Distinct().ToList();
        var taskRows = moduleIds.Count == 0 || recordIds.Count == 0 ? new List<EscalationTaskContext>()
            : await _context.Tasks.AsNoTracking().Where(t => !t.IsDeleted && moduleIds.Contains(t.BusinessModuleId) && recordIds.Contains(t.BusinessRecordId))
                .Select(t => new EscalationTaskContext(t.Id, t.BusinessModuleId, t.BusinessRecordId, t.Task)).ToListAsync(ct);
        var tasks = taskRows.GroupBy(t => (t.BusinessModuleId, t.BusinessRecordId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.Id).First());

        for (var i = 0; i < escalations.Count; i++)
        {
            var escalation = escalations[i];
            var dto = dtos[i];
            if (levels.TryGetValue(escalation.EscalationLevelId, out var level))
            {
                dto.EscalationLevelName = level.Name;
                dto.EscalationLevelNumber = level.Level;
            }
            dto.EscalationState = escalation.ResolvedAt != null ? "Resolved" : escalation.AcknowledgedAt != null ? "Acknowledged" : "Open";
            dto.IsAcknowledged = escalation.AcknowledgedAt.HasValue;
            dto.IsResolved = escalation.ResolvedAt.HasValue;
            if (!escalation.FollowupId.HasValue || !followups.TryGetValue(escalation.FollowupId.Value, out var followup)) continue;

            dto.ReminderAt = followup.ReminderAt;
            dto.Remark = followup.Note;
            dto.ReminderRecipientEmployeeId = followup.ReminderRecipientEmployeeId;
            dto.ReminderRecipientName = followup.ReminderRecipientName;
            if (!followup.BusinessModuleId.HasValue) continue;
            dto.ModuleName = modules.GetValueOrDefault(followup.BusinessModuleId.Value);
            if (followup.BusinessRecordId != null && tasks.TryGetValue((followup.BusinessModuleId.Value, followup.BusinessRecordId), out var task))
            {
                dto.EaTaskId = task.Id;
                dto.Task = task.Task;
            }
        }
        return dtos;
    }
    public async Task ResolveAsync(long id, ResolveEscalationRequestDto dto, CancellationToken ct = default)
    {
        var e = await _repo.GetByIdAsync(id, ct) ?? throw new Jarvis5.Common.NotFoundException($"Escalation {id} not found.");
        var oldSnap = new { e.Id, e.ResolvedAt };
        if (e.ResolvedAt != null) throw new Jarvis5.Common.BadRequestException("Escalation already resolved.");
        e.ResolvedAt = Clock.UtcNowTz;
        e.ResolvedById = _currentUser.ActorId();
        var resolvedByName = _currentUser.UserName;
        e.ResolutionNote = dto?.ResolutionNote?.Trim();
        e.ModifiedBy = _currentUser.ActorDisplay();
        e.ModifiedDate = Clock.UtcNowTz;
        // Escalation entity does not include ModifiedBy/ModifiedDate in the current model,
        // so only set ResolvedAt and persist.
        _repo.Update(e);

        _auditService.AddAudit(
            actionType: "ESCALATION_RESOLVE",
            module: "Escalation",
            entityName: nameof(Escalation),
            entityId: e.Id.ToString(),
            oldValues: oldSnap,
            newValues: new { e.Id, e.ResolvedAt, ResolvedById = e.ResolvedById, ResolvedByName = resolvedByName, ResolutionNote = e.ResolutionNote },
            description: "Escalation resolved");

        await _context.SaveChangesAsync(ct);
    }

    public async Task AcknowledgeAsync(long id, string? note, CancellationToken ct = default)
    {
        var e = await _repo.GetByIdAsync(id, ct) ?? throw new Jarvis5.Common.NotFoundException($"Escalation {id} not found.");
        if (e.ResolvedAt != null) throw new Jarvis5.Common.BadRequestException("Cannot acknowledge a resolved escalation.");
        if (e.AcknowledgedAt != null) throw new Jarvis5.Common.BadRequestException("Escalation already acknowledged.");
        e.AcknowledgedAt = Clock.UtcNowTz;
        e.AcknowledgedById = _currentUser.ActorId();
        var ackByName = _currentUser.UserName;
        e.AcknowledgementNote = note?.Trim();
        e.ModifiedBy = _currentUser.ActorDisplay();
        e.ModifiedDate = Clock.UtcNowTz;
        _repo.Update(e);

        _auditService.AddAudit(
            actionType: "ESCALATION_ACKNOWLEDGE",
            module: "Escalation",
            entityName: nameof(Escalation),
            entityId: e.Id.ToString(),
            oldValues: new { e.Id, e.AcknowledgedAt },
            newValues: new { e.Id, e.AcknowledgedAt, AcknowledgedById = e.AcknowledgedById, AcknowledgedByName = ackByName, AcknowledgementNote = e.AcknowledgementNote },
            description: "Escalation acknowledged");

        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<EscalationLevelResponseDto>> GetActiveLevelsAsync(CancellationToken ct = default)
    {
        var levels = await _repo.GetActiveLevelsAsync(ct);
        return levels
            .Select(l => new EscalationLevelResponseDto
            {
                Id = l.Id,
                Code = l.Code,
                Name = l.Name,
                Description = l.Description,
                Level = l.Level
            }).ToList();
    }
}
