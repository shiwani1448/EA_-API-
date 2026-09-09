using AutoMapper;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public class IntakeService : IIntakeService
{
    private readonly IIntakeRepository _repo;
    private readonly EaFmsDbContext _context;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;

    public IntakeService(
        IIntakeRepository repo,
        EaFmsDbContext context,
        IMapper mapper,
        ICurrentUserService currentUser,
        IAuditService auditService)
    {
        _repo = repo;
        _context = context;
        _mapper = mapper;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<IntakeRequestResponseDto> CreateAsync(CreateIntakeRequestDto dto, CancellationToken ct = default)
    {
        // validate referenced catalogs
        if (dto.BusinessModuleId.HasValue)
        {
            var bm = await _context.BusinessModules.FirstOrDefaultAsync(b => b.Id == dto.BusinessModuleId.Value && b.IsActive && !b.IsDeleted, ct);
            if (bm is null) throw new BadHttpRequestException("BusinessModule not found or inactive.");
        }

        if (dto.StatusId.HasValue)
        {
            var st = await _context.Statuses.FirstOrDefaultAsync(s => s.Id == dto.StatusId.Value && s.IsActive, ct);
            if (st is null) throw new BadHttpRequestException("Status not found or inactive.");
        }

        if (dto.PriorityLevelId.HasValue)
        {
            var pr = await _context.PriorityLevels.FirstOrDefaultAsync(p => p.Id == dto.PriorityLevelId.Value && p.IsActive, ct);
            if (pr is null) throw new BadHttpRequestException("PriorityLevel not found or inactive.");
        }

        var now = Clock.UtcNowTz;
        var by = _currentUser.UserName ?? _currentUser.UserId.ToString();

        var entity = new IntakeRequest
        {
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim(),
            RequiredDate = dto.RequiredDate,
            BusinessModuleId = dto.BusinessModuleId,
            StatusId = dto.StatusId,
            PriorityLevelId = dto.PriorityLevelId,
            IsActive = true,
            IsDeleted = false,
            CreatedBy = by,
            CreatedDate = now
        };

        await _repo.AddAsync(entity, ct);
        // add audit entry (do not SaveChanges inside audit service)
        _auditService.AddAudit(
            actionType: "CREATE",
            module: "Intake",
            entityName: nameof(IntakeRequest),
            entityId: entity.Id.ToString(),
            oldValues: null,
            newValues: new { entity.Id, entity.Title, entity.StatusId, entity.BusinessModuleId },
            description: "Intake created");

        await _context.SaveChangesAsync(ct);

        return await GetByIdAsync(entity.Id, ct);
    }

    public async Task<IntakeRequestResponseDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var e = await _repo.GetByIdAsync(id, ct) ?? throw new Jarvis5.Common.NotFoundException($"Intake {id} not found.");
        var dto = _mapper.Map<IntakeRequestResponseDto>(e);

        if (e.BusinessModuleId.HasValue)
        {
            var bm = await _context.BusinessModules.FindAsync(new object[] { e.BusinessModuleId.Value }, ct);
            dto.BusinessModuleName = bm?.Name;
        }

        if (e.StatusId.HasValue)
        {
            var st = await _context.Statuses.FindAsync(new object[] { e.StatusId.Value }, ct);
            dto.StatusName = st?.Name;
        }

        if (e.PriorityLevelId.HasValue)
        {
            var pr = await _context.PriorityLevels.FindAsync(new object[] { e.PriorityLevelId.Value }, ct);
            dto.PriorityLevelName = pr?.Name;
        }

        return dto;
    }

    public async Task<(List<IntakeRequestResponseDto> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? search, CancellationToken ct = default)
    {
        var (items, total) = await _repo.GetPagedAsync(pageNumber, pageSize, search, ct);
        var dtos = items.Select(i => _mapper.Map<IntakeRequestResponseDto>(i)).ToList();

        // populate catalog names
        foreach (var dto in dtos)
        {
            if (dto.BusinessModuleId.HasValue)
            {
                var bm = await _context.BusinessModules.FindAsync(new object[] { dto.BusinessModuleId.Value }, ct);
                dto.BusinessModuleName = bm?.Name;
            }

            if (dto.StatusId.HasValue)
            {
                var st = await _context.Statuses.FindAsync(new object[] { dto.StatusId.Value }, ct);
                dto.StatusName = st?.Name;
            }

            if (dto.PriorityLevelId.HasValue)
            {
                var pr = await _context.PriorityLevels.FindAsync(new object[] { dto.PriorityLevelId.Value }, ct);
                dto.PriorityLevelName = pr?.Name;
            }
        }

        return (dtos, total);
    }

    public async Task<IntakeRequestResponseDto> UpdateAsync(long id, UpdateIntakeRequestDto dto, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct) ?? throw new Jarvis5.Common.NotFoundException($"Intake {id} not found.");

        if (dto.BusinessModuleId.HasValue)
        {
            var bm = await _context.BusinessModules.FirstOrDefaultAsync(b => b.Id == dto.BusinessModuleId.Value && b.IsActive && !b.IsDeleted, ct);
            if (bm is null) throw new BadHttpRequestException("BusinessModule not found or inactive.");
        }

        if (dto.StatusId.HasValue)
        {
            var st = await _context.Statuses.FirstOrDefaultAsync(s => s.Id == dto.StatusId.Value && s.IsActive, ct);
            if (st is null) throw new BadHttpRequestException("Status not found or inactive.");
        }

        if (dto.PriorityLevelId.HasValue)
        {
            var pr = await _context.PriorityLevels.FirstOrDefaultAsync(p => p.Id == dto.PriorityLevelId.Value && p.IsActive, ct);
            if (pr is null) throw new BadHttpRequestException("PriorityLevel not found or inactive.");
        }

        // capture old snapshot
        var oldSnapshot = new { entity.Id, entity.Title, entity.StatusId, entity.BusinessModuleId };

        entity.Title = dto.Title.Trim();
        entity.Description = dto.Description?.Trim();
        entity.RequiredDate = dto.RequiredDate;
        entity.BusinessModuleId = dto.BusinessModuleId;
        entity.StatusId = dto.StatusId;
        entity.PriorityLevelId = dto.PriorityLevelId;
        entity.ModifiedBy = _currentUser.UserName ?? _currentUser.UserId.ToString();
        entity.ModifiedDate = Clock.UtcNowTz;

        _repo.Update(entity);

        // add audit entry
        _auditService.AddAudit(
            actionType: "UPDATE",
            module: "Intake",
            entityName: nameof(IntakeRequest),
            entityId: entity.Id.ToString(),
            oldValues: oldSnapshot,
            newValues: new { entity.Id, entity.Title, entity.StatusId, entity.BusinessModuleId },
            description: "Intake updated");

        await _context.SaveChangesAsync(ct);

        return await GetByIdAsync(entity.Id, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct) ?? throw new Jarvis5.Common.NotFoundException($"Intake {id} not found.");
        // capture old snapshot
        var oldSnap = new { entity.Id, entity.Title, entity.IsDeleted };
        entity.IsDeleted = true;
        entity.ModifiedBy = _currentUser.UserName ?? _currentUser.UserId.ToString();
        entity.ModifiedDate = Clock.UtcNowTz;
        _repo.Update(entity);

        _auditService.AddAudit(
            actionType: "DELETE",
            module: "Intake",
            entityName: nameof(IntakeRequest),
            entityId: entity.Id.ToString(),
            oldValues: oldSnap,
            newValues: new { entity.Id, entity.IsDeleted },
            description: "Intake soft-deleted");

        await _context.SaveChangesAsync(ct);
    }

    public async Task<IntakeClassificationResponseDto> CreateClassificationAsync(long intakeId, CreateIntakeClassificationDto dto, CancellationToken ct = default)
    {
        var intake = await _repo.GetByIdAsync(intakeId, ct) ?? throw new Jarvis5.Common.NotFoundException($"Intake {intakeId} not found.");

        var now = Clock.UtcNowTz;
        var by = _currentUser.UserName ?? _currentUser.UserId.ToString();

        var c = new IntakeClassification
        {
            IntakeRequestId = intakeId,
            Name = dto.Name.Trim(),
            Details = dto.Details?.Trim(),
            CreatedBy = by,
            CreatedDate = now
        };

        await _repo.AddClassificationAsync(c, ct);
        await _context.SaveChangesAsync(ct);

        return _mapper.Map<IntakeClassificationResponseDto>(c);
    }

    public async Task<List<IntakeClassificationResponseDto>> GetClassificationsAsync(long intakeId, CancellationToken ct = default)
    {
        var intake = await _repo.GetByIdAsync(intakeId, ct) ?? throw new Jarvis5.Common.NotFoundException($"Intake {intakeId} not found.");
        var list = await _repo.GetClassificationsAsync(intakeId, ct);
        return list.Select(l => _mapper.Map<IntakeClassificationResponseDto>(l)).ToList();
    }

    public async Task<IntakeClassificationResponseDto> UpdateClassificationAsync(long intakeId, long classificationId, UpdateIntakeClassificationDto dto, CancellationToken ct = default)
    {
        var intake = await _repo.GetByIdAsync(intakeId, ct) ?? throw new Jarvis5.Common.NotFoundException($"Intake {intakeId} not found.");
        var c = await _repo.GetClassificationByIdAsync(classificationId, ct) ?? throw new Jarvis5.Common.NotFoundException($"Classification {classificationId} not found.");
        if (c.IntakeRequestId != intakeId) throw new BadHttpRequestException("Classification does not belong to the intake.");

        c.Name = dto.Name.Trim();
        c.Details = dto.Details?.Trim();
        c.ModifiedBy = _currentUser.UserName ?? _currentUser.UserId.ToString();
        c.ModifiedDate = Clock.UtcNowTz;

        _repo.UpdateClassification(c);
        await _context.SaveChangesAsync(ct);

        return _mapper.Map<IntakeClassificationResponseDto>(c);
    }

    public async Task DeleteClassificationAsync(long intakeId, long classificationId, CancellationToken ct = default)
    {
        var intake = await _repo.GetByIdAsync(intakeId, ct) ?? throw new Jarvis5.Common.NotFoundException($"Intake {intakeId} not found.");
        var c = await _repo.GetClassificationByIdAsync(classificationId, ct) ?? throw new Jarvis5.Common.NotFoundException($"Classification {classificationId} not found.");
        if (c.IntakeRequestId != intakeId) throw new BadHttpRequestException("Classification does not belong to the intake.");

        _repo.DeleteClassification(c);
        await _context.SaveChangesAsync(ct);
    }
}
