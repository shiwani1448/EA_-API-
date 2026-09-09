using AutoMapper;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public class WorkflowService : IWorkflowService
{
    private readonly IWorkflowRepository _repo;
    private readonly EaFmsDbContext _context;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;

    public WorkflowService(IWorkflowRepository repo, EaFmsDbContext context, IMapper mapper, ICurrentUserService currentUser, IAuditService auditService)
    {
        _repo = repo;
        _context = context;
        _mapper = mapper;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<WorkflowResponseDto> StartAsync(StartWorkflowRequestDto dto, CancellationToken ct = default)
    {
        if (dto.IntakeRequestId <= 0) throw new ArgumentException("IntakeRequestId must be provided.");

        var intake = await _context.IntakeRequests.FirstOrDefaultAsync(i => i.Id == dto.IntakeRequestId && !i.IsDeleted, ct);
        if (intake is null) throw new Jarvis5.Common.NotFoundException($"Intake {dto.IntakeRequestId} not found.");

        var existing = await _repo.GetByIntakeRequestIdAsync(dto.IntakeRequestId, ct);
        if (existing is not null) throw new InvalidOperationException("An active workflow already exists for this intake.");

        return await CreateCapturedAsync(dto.IntakeRequestId, null, null, dto.Notes, ct);
    }

    public async Task<WorkflowResponseDto> GetOrCreateForBusinessRecordAsync(long businessModuleId, string businessRecordId, long? intakeRequestId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(businessRecordId) || businessRecordId.Length > 200)
            throw new ArgumentException("A business record ID of at most 200 characters is required.");
        if (!await _context.BusinessModules.AnyAsync(m => m.Id == businessModuleId && m.IsActive && !m.IsDeleted, ct))
            throw new InvalidOperationException("The business module is missing or inactive.");

        await using var transaction = _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync(ct) : null;
        // Serialize creation for this generic source, including concurrent service calls.
        var sourceKey = $"ea-workflow:{businessModuleId}:{businessRecordId}";
        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({sourceKey}, 0))", ct);
        var matches = await _context.WorkflowInstances
            .Where(w => w.BusinessModuleId == businessModuleId && w.BusinessRecordId == businessRecordId && !w.IsDeleted)
            .Take(2).ToListAsync(ct);
        if (matches.Count > 1) throw new InvalidOperationException("Multiple workflows reference this business record.");
        var result = matches.Count == 1
            ? await GetByIdAsync(matches[0].Id, ct)
            : await CreateCapturedAsync(intakeRequestId, businessModuleId, businessRecordId, null, ct);
        if (transaction != null) await transaction.CommitAsync(ct);
        return result;
    }

    private async Task<WorkflowResponseDto> CreateCapturedAsync(long? intakeRequestId, long? businessModuleId, string? businessRecordId, string? notes, CancellationToken ct)
    {
        // Determine initial status 'Captured'
        var initialStatus = await _context.Statuses.FirstOrDefaultAsync(s => EF.Functions.ILike(s.Name, "Captured") && s.IsActive && !s.IsDeleted, ct);
        if (initialStatus is null)
            throw new InvalidOperationException("Initial workflow status 'Captured' not found or inactive.");

        var now = Clock.UtcNowTz;
        var by = _currentUser.UserName ?? _currentUser.UserId.ToString();

        var instance = new WorkflowInstance
        {
            IntakeRequestId = intakeRequestId,
            BusinessModuleId = businessModuleId,
            BusinessRecordId = businessRecordId,
            StatusId = initialStatus.Id,
            StartedAt = now,
            UpdatedAt = now,
            IsActive = true,
            IsDeleted = false,
            CreatedBy = by,
            CreatedDate = now
        };

        // Start workflow: need transaction since we save instance then history. Include audit in same transaction.
        await using var tx = _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync(ct) : null;
        try
        {
            await _repo.AddAsync(instance, ct);
            await _context.SaveChangesAsync(ct); // populate instance.Id

            var history = new WorkflowHistory
            {
                WorkflowInstanceId = instance.Id,
                FromStatusId = null,
                ToStatusId = initialStatus.Id,
                Notes = notes,
                ChangedAt = now,
                CreatedBy = by,
                CreatedDate = now
            };

            await _repo.AddHistoryAsync(history, ct);

            // audit: Captured lifecycle created (not execution start — that is POST .../start)
            _auditService.AddAudit(
                actionType: "WORKFLOW_CAPTURE",
                module: "Workflow",
                entityName: nameof(WorkflowInstance),
                entityId: instance.Id.ToString(),
                oldValues: null,
                newValues: new { instance.Id, instance.IntakeRequestId, instance.BusinessModuleId, instance.BusinessRecordId, instance.StatusId },
                description: "Captured workflow created");

            await _context.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync(ct);
            throw;
        }

        return await GetByIdAsync(instance.Id, ct);
    }

    public async Task<WorkflowResponseDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var instance = await _repo.GetByIdAsync(id, ct) ?? throw new Jarvis5.Common.NotFoundException($"Workflow {id} not found.");
        var dto = _mapper.Map<WorkflowResponseDto>(instance);

        var status = await _context.Statuses.FindAsync(new object[] { instance.StatusId }, ct);
        dto.StatusName = status?.Name;

        dto.History = instance.History
            .OrderBy(h => h.ChangedAt)
            .Select(h => new WorkflowHistoryResponseDto
            {
                Id = h.Id,
                WorkflowInstanceId = h.WorkflowInstanceId,
                FromStatusId = h.FromStatusId,
                FromStatusName = h.FromStatusId.HasValue ? _context.Statuses.Find(h.FromStatusId.Value)?.Name : null,
                ToStatusId = h.ToStatusId,
                ToStatusName = h.ToStatusId.HasValue ? _context.Statuses.Find(h.ToStatusId.Value)?.Name : null,
                Notes = h.Notes,
                ChangedAt = h.ChangedAt,
                CreatedBy = h.CreatedBy,
                CreatedDate = h.CreatedDate
            })
            .ToList();

        return dto;
    }

    public async Task<WorkflowResponseDto?> GetByIntakeRequestIdAsync(long intakeRequestId, CancellationToken ct = default)
    {
        var instance = await _repo.GetByIntakeRequestIdAsync(intakeRequestId, ct);
        if (instance is null) return null;
        return await GetByIdAsync(instance.Id, ct);
    }

    public async Task<WorkflowResponseDto> TransitionAsync(long id, TransitionWorkflowRequestDto dto, CancellationToken ct = default, bool preserveTatStartedAt = false)
    {
        if (dto.TargetStatusId <= 0) throw new ArgumentException("TargetStatusId must be provided.");

        var instance = await _repo.GetByIdAsync(id, ct) ?? throw new Jarvis5.Common.NotFoundException($"Workflow {id} not found.");

        var target = await _context.Statuses.FirstOrDefaultAsync(s => s.Id == dto.TargetStatusId && s.IsActive && !s.IsDeleted, ct);
        if (target is null) throw new Jarvis5.Common.NotFoundException($"Target status {dto.TargetStatusId} not found or inactive.");

        var currentStatus = await _context.Statuses.FirstOrDefaultAsync(s => s.Id == instance.StatusId, ct);
        var isCurrentCompleted = currentStatus != null
            && string.Equals(currentStatus.Name, "Completed", StringComparison.OrdinalIgnoreCase);
        var isTargetCompleted = string.Equals(target.Name, "Completed", StringComparison.OrdinalIgnoreCase);

        // Completed is terminal for active execution. Reject repeated Complete and exits
        // to Captured / In Progress / Waiting / Follow-up / Submitted (and any other transition).
        if (isCurrentCompleted)
        {
            throw new BusinessRuleException(
                isTargetCompleted
                    ? "Workflow is already Completed."
                    : "Completed workflow cannot transition back to an active execution status.");
        }

        if (isTargetCompleted)
        {
            var hasOpenPause = await _context.WorkPauses.AnyAsync(
                p => p.WorkflowInstanceId == instance.Id && !p.IsDeleted && p.EndAt == null, ct);
            if (hasOpenPause)
                throw new BusinessRuleException(
                    "Cannot complete workflow while an open WorkPause exists. Resume the pause first.");
        }

        var fromStatusId = instance.StatusId;
        var oldSnapshot = new { instance.Id, instance.StatusId };
        var now = Clock.UtcNowTz;
        instance.StatusId = dto.TargetStatusId;
        instance.UpdatedAt = now;

        // First transition into In Progress establishes authoritative execution start (TatStartedAt).
        // Do not overwrite on later re-entry; do not treat workflow creation / Captured as execution start.
        if (string.Equals(target.Name, "In Progress", StringComparison.OrdinalIgnoreCase)
            && !instance.TatStartedAt.HasValue && !preserveTatStartedAt)
        {
            instance.TatStartedAt = now;
        }

        if (isTargetCompleted ||
            string.Equals(target.Name, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            // First successful completion timestamp only; never overwrite CompletedAt.
            if (!instance.CompletedAt.HasValue)
                instance.CompletedAt = now;
            instance.IsActive = false;
        }

        instance.ModifiedBy = _currentUser.UserName ?? _currentUser.UserId.ToString();
        instance.ModifiedDate = now;

        _repo.Update(instance);

        // add history and audit in a transaction to ensure atomicity
        await using var tran = _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync(ct) : null;
        try
        {
            await _context.SaveChangesAsync(ct);

            var history = new WorkflowHistory
            {
                WorkflowInstanceId = instance.Id,
                FromStatusId = fromStatusId,
                ToStatusId = dto.TargetStatusId,
                Notes = dto.Notes,
                ChangedAt = now,
                CreatedBy = _currentUser.UserName ?? _currentUser.UserId.ToString(),
                CreatedDate = now
            };

            await _repo.AddHistoryAsync(history, ct);

            _auditService.AddAudit(
                actionType: "WORKFLOW_TRANSITION",
                module: "Workflow",
                entityName: nameof(WorkflowInstance),
                entityId: instance.Id.ToString(),
                oldValues: oldSnapshot,
                newValues: new { instance.Id, instance.StatusId },
                description: dto.Notes);

            await _context.SaveChangesAsync(ct);
            if (tran != null) await tran.CommitAsync(ct);
        }
        catch
        {
            if (tran != null) await tran.RollbackAsync(ct);
            throw;
        }

        return await GetByIdAsync(instance.Id, ct);
    }

    public async Task<List<WorkflowHistoryResponseDto>> GetHistoryAsync(long id, CancellationToken ct = default)
    {
        if (!await _context.WorkflowInstances.AnyAsync(w => w.Id == id && !w.IsDeleted, ct))
            throw new NotFoundException($"Workflow {id} not found.");
        var histories = await _repo.GetHistoryAsync(id, ct);
        var list = new List<WorkflowHistoryResponseDto>();
        foreach (var h in histories)
        {
            var fromName = h.FromStatusId.HasValue ? (await _context.Statuses.FindAsync(new object[] { h.FromStatusId.Value }, ct))?.Name : null;
            var toName = h.ToStatusId.HasValue ? (await _context.Statuses.FindAsync(new object[] { h.ToStatusId.Value }, ct))?.Name : null;
            list.Add(new WorkflowHistoryResponseDto
            {
                Id = h.Id,
                WorkflowInstanceId = h.WorkflowInstanceId,
                FromStatusId = h.FromStatusId,
                FromStatusName = fromName,
                ToStatusId = h.ToStatusId,
                ToStatusName = toName,
                Notes = h.Notes,
                ChangedAt = h.ChangedAt,
                CreatedBy = h.CreatedBy,
                CreatedDate = h.CreatedDate
            });
        }

        return list.OrderBy(h => h.ChangedAt).ToList();
    }
}
