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
        var by = _currentUser.UserName ?? _currentUser.UserId.ToString();

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

    public async Task ResolveAsync(long id, ResolveEscalationRequestDto dto, CancellationToken ct = default)
    {
        var e = await _repo.GetByIdAsync(id, ct) ?? throw new Jarvis5.Common.NotFoundException($"Escalation {id} not found.");
        var oldSnap = new { e.Id, e.ResolvedAt };
        if (e.ResolvedAt != null) throw new Jarvis5.Common.BadRequestException("Escalation already resolved.");
        e.ResolvedAt = Clock.UtcNowTz;
        e.ResolvedById = _currentUser.UserId.ToString();
        var resolvedByName = _currentUser.UserName;
        e.ResolutionNote = dto?.ResolutionNote?.Trim();
        e.ModifiedBy = _currentUser.UserName ?? _currentUser.UserId.ToString();
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
        e.AcknowledgedById = _currentUser.UserId.ToString();
        var ackByName = _currentUser.UserName;
        e.AcknowledgementNote = note?.Trim();
        e.ModifiedBy = _currentUser.UserName ?? _currentUser.UserId.ToString();
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

    public Task<List<EscalationLevelResponseDto>> GetActiveLevelsAsync(CancellationToken ct = default)
    {
        return _repo.GetActiveLevelsAsync(ct).ContinueWith(task => task.Result
            .Select(l => new EscalationLevelResponseDto
            {
                Id = l.Id,
                Code = l.Code,
                Name = l.Name,
                Description = l.Description,
                Level = l.Level
            }).ToList(), ct);
    }
}
