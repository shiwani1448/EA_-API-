using AutoMapper;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public class FollowupService : IFollowupService
{
    private readonly IFollowupRepository _repo;
    private readonly EaFmsDbContext _context;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;
    private readonly IFollowupSourceResolver _sourceResolver;

    public FollowupService(IFollowupRepository repo, EaFmsDbContext context, IMapper mapper, ICurrentUserService currentUser, IAuditService auditService, IFollowupSourceResolver sourceResolver)
    {
        _repo = repo;
        _context = context;
        _mapper = mapper;
        _currentUser = currentUser;
        _auditService = auditService;
        _sourceResolver = sourceResolver;
    }

    public async Task<FollowupResponseDto> CreateAsync(CreateFollowupRequestDto dto, CancellationToken ct = default)
    {
        var validation = new Jarvis5.Validators.CreateFollowupRequestDtoValidator().Validate(dto);
        if (!validation.IsValid) throw new BadRequestException(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        if (dto.IntakeRequestId.HasValue && !await _context.IntakeRequests.AnyAsync(i => i.Id == dto.IntakeRequestId && !i.IsDeleted, ct))
            throw new NotFoundException($"Intake {dto.IntakeRequestId} not found.");
        if (dto.BusinessModuleId.HasValue)
        {
            var module = await _context.BusinessModules.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dto.BusinessModuleId && x.IsActive && !x.IsDeleted, ct)
                ?? throw new NotFoundException($"Business module {dto.BusinessModuleId} not found or inactive.");
            dto.BusinessRecordId = dto.BusinessRecordId!.Trim();
            if (string.Equals(module.Name.Trim(), "Meeting", StringComparison.OrdinalIgnoreCase))
            {
                if (!long.TryParse(dto.BusinessRecordId, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var meetingId))
                    throw new BadRequestException("Meeting BusinessRecordId must be a positive integer.");
                var meeting = await _context.Meetings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == meetingId && !x.IsDeleted, ct)
                    ?? throw new NotFoundException($"Meeting {meetingId} not found.");
                if ((dto.IntakeRequestId.HasValue && dto.IntakeRequestId != meeting.IntakeRequestId)
                    || (dto.WorkflowInstanceId.HasValue && dto.WorkflowInstanceId != meeting.WorkflowInstanceId))
                    throw new BadRequestException("Follow-up intake/workflow source does not match the meeting.");
                dto.BusinessRecordId = meetingId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            // Other active modules retain their source IDs; record validation awaits their implementation.
        }

        if (dto.WorkflowInstanceId.HasValue)
        {
            var wf = await _context.WorkflowInstances.FirstOrDefaultAsync(w => w.Id == dto.WorkflowInstanceId.Value && !w.IsDeleted, ct);
            if (wf is null) throw new Jarvis5.Common.NotFoundException($"Workflow {dto.WorkflowInstanceId} not found.");
            if (dto.BusinessModuleId.HasValue && wf.BusinessModuleId.HasValue
                && (dto.BusinessModuleId != wf.BusinessModuleId || dto.BusinessRecordId != wf.BusinessRecordId))
                throw new BadRequestException("Follow-up source does not match the workflow source.");
            // ensure workflow belongs to intake
            if (dto.IntakeRequestId.HasValue && wf.IntakeRequestId != dto.IntakeRequestId) throw new Jarvis5.Common.BadRequestException("Workflow does not belong to intake.");
        }

        if (dto.PriorityLevelId.HasValue)
        {
            var pr = await _context.PriorityLevels.FirstOrDefaultAsync(p => p.Id == dto.PriorityLevelId.Value && p.IsActive && !p.IsDeleted, ct);
            if (pr is null) throw new Jarvis5.Common.NotFoundException($"PriorityLevel {dto.PriorityLevelId} not found or inactive.");
        }

        var now = Clock.UtcNowTz;
        var by = _currentUser.UserName ?? _currentUser.UserId.ToString();

        var followup = new Followup
        {
            BusinessModuleId = dto.BusinessModuleId,
            BusinessRecordId = dto.BusinessRecordId,
            IntakeRequestId = dto.IntakeRequestId,
            WorkflowInstanceId = dto.WorkflowInstanceId,
            Subject = dto.Subject?.Trim(),
            Type = dto.Type?.Trim(),
            DueAt = dto.DueAt,
            Note = dto.Note?.Trim(),
            AssignedToId = dto.AssignedToId,
            AssignedToName = dto.AssignedToName,
            PriorityLevelId = dto.PriorityLevelId,
            ReminderAt = dto.ReminderAt,
            NextFollowupAt = dto.NextFollowupAt,
            ResponseOwnerId = dto.ResponseOwnerId,
            ResponseOwnerName = dto.ResponseOwnerName,
            ExpectedResponseAt = dto.ExpectedResponseAt,
            WaitingOnId = dto.WaitingOnId,
            WaitingOnName = dto.WaitingOnName,
            WaitingOnExternal = dto.WaitingOnExternal,
            SequenceNumber = dto.SequenceNumber,
            IsDeleted = false,
            CreatedBy = by,
            CreatedDate = now
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        await _repo.AddAsync(followup, ct);
        await _context.SaveChangesAsync(ct);

        _auditService.AddAudit(
            actionType: "FOLLOWUP_CREATE",
            module: "Followup",
            entityName: nameof(Followup),
            entityId: followup.Id.ToString(),
            oldValues: null,
            newValues: new
            {
                followup.Id,
                followup.BusinessModuleId,
                followup.BusinessRecordId,
                followup.IntakeRequestId,
                followup.WorkflowInstanceId,
                followup.Subject,
                followup.Type,
                followup.Note,
                followup.AssignedToId,
                followup.AssignedToName,
                followup.PriorityLevelId,
                followup.DueAt,
                followup.ReminderAt,
                LastFollowupAt = (DateTime?)null,
                followup.NextFollowupAt,
                followup.WaitingOnId,
                followup.WaitingOnName,
                followup.WaitingOnExternal,
                followup.ResponseOwnerId,
                followup.ResponseOwnerName,
                followup.ExpectedResponseAt,
                followup.SequenceNumber
            },
            description: "Followup created");

        await _context.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
        return await EnrichAsync(followup, ct);
    }

    public async Task<FollowupResponseDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var f = await _repo.GetByIdAsync(id, ct) ?? throw new Jarvis5.Common.NotFoundException($"Followup {id} not found.");
        return await EnrichAsync(f, ct);
    }

    public async Task<List<FollowupResponseDto>> GetByIntakeRequestIdAsync(long intakeRequestId, CancellationToken ct = default)
    {
        var list = await _repo.GetByIntakeRequestIdAsync(intakeRequestId, ct);
        return await EnrichListAsync(list, ct);
    }

    public async Task<FollowupResponseDto> UpdateAsync(long id, UpdateFollowupRequestDto dto, CancellationToken ct = default)
    {
        var f = await _repo.GetByIdAsync(id, ct) ?? throw new Jarvis5.Common.NotFoundException($"Followup {id} not found.");
        if (dto.PriorityLevelId.HasValue && dto.PriorityLevelId != f.PriorityLevelId
            && !await _context.PriorityLevels.AnyAsync(p => p.Id == dto.PriorityLevelId && p.IsActive && !p.IsDeleted, ct))
            throw new NotFoundException($"PriorityLevel {dto.PriorityLevelId} not found or inactive.");
        var oldSnap = new { f.Id, f.DueAt, f.Note };
        f.Subject = dto.Subject?.Trim();
        f.Type = dto.Type?.Trim();
        f.DueAt = dto.DueAt;
        f.Note = dto.Note?.Trim();
        f.AssignedToId = dto.AssignedToId;
        f.AssignedToName = dto.AssignedToName;
        f.PriorityLevelId = dto.PriorityLevelId;
        f.ReminderAt = dto.ReminderAt;
        f.NextFollowupAt = dto.NextFollowupAt;
        f.ResponseOwnerId = dto.ResponseOwnerId;
        f.ResponseOwnerName = dto.ResponseOwnerName;
        f.ExpectedResponseAt = dto.ExpectedResponseAt;
        f.WaitingOnId = dto.WaitingOnId;
        f.WaitingOnName = dto.WaitingOnName;
        f.WaitingOnExternal = dto.WaitingOnExternal;
        f.SequenceNumber = dto.SequenceNumber;
        f.ModifiedBy = _currentUser.UserName ?? _currentUser.UserId.ToString();
        f.ModifiedDate = Clock.UtcNowTz;

        _repo.Update(f);

        _auditService.AddAudit(
            actionType: "FOLLOWUP_UPDATE",
            module: "Followup",
            entityName: nameof(Followup),
            entityId: f.Id.ToString(),
            oldValues: oldSnap,
            newValues: new
            {
                f.Id,
                f.Subject,
                f.Type,
                f.Note,
                f.AssignedToId,
                f.AssignedToName,
                f.PriorityLevelId,
                f.DueAt,
                f.ReminderAt,
                f.NextFollowupAt,
                f.WaitingOnId,
                f.WaitingOnName,
                f.WaitingOnExternal,
                f.ResponseOwnerId,
                f.ResponseOwnerName,
                f.ExpectedResponseAt,
                f.SequenceNumber,
                f.ModifiedBy,
                f.ModifiedDate
            },
            description: "Followup updated");

        await _context.SaveChangesAsync(ct);

        return await EnrichAsync(f, ct);
    }

    public async Task<FollowupResponseDto> CompleteAsync(long id, CompleteFollowupRequestDto dto, CancellationToken ct = default)
    {
        var f = await _repo.GetByIdAsync(id, ct) ?? throw new Jarvis5.Common.NotFoundException($"Followup {id} not found.");
        if (f.CompletedAt.HasValue) throw new BadRequestException("Follow-up already completed.");
        var oldSnap2 = new { f.Id, f.CompletedAt };
        // apply completion data: client may suggest CompletionNote/OutcomeCode but actor/time are server-controlled
        f.CompletionNote = dto.CompletionNote?.Trim();
        f.OutcomeCode = dto.OutcomeCode?.Trim();
        f.CompletedAt = Clock.UtcNowTz;
        f.CompletedById = _currentUser.UserId.ToString();
        f.CompletedByName = _currentUser.UserName;
        f.ModifiedBy = _currentUser.UserName ?? _currentUser.UserId.ToString();
        f.ModifiedDate = Clock.UtcNowTz;

        _repo.Update(f);

        _auditService.AddAudit(
            actionType: "FOLLOWUP_COMPLETE",
            module: "Followup",
            entityName: nameof(Followup),
            entityId: f.Id.ToString(),
            oldValues: oldSnap2,
            newValues: new
            {
                f.Id,
                f.CompletedAt,
                f.CompletedById,
                f.CompletedByName,
                f.CompletionNote,
                f.OutcomeCode,
                f.ModifiedBy,
                f.ModifiedDate
            },
            description: "Followup completed");

        await _context.SaveChangesAsync(ct);
        return await EnrichAsync(f, ct);
    }

    public async Task RecordFollowupAsync(long id, RecordFollowupRequestDto dto, CancellationToken ct = default)
    {
        var f = await _repo.GetByIdAsync(id, ct) ?? throw new Jarvis5.Common.NotFoundException($"Followup {id} not found.");
        if (f.IsDeleted) throw new Jarvis5.Common.NotFoundException($"Followup {id} not found.");

        if (f.CompletedAt.HasValue) throw new BadRequestException("Cannot record an attempt on a completed follow-up.");
        if (dto.Note?.Length > 2000) throw new BadRequestException("Note must not exceed 2000 characters.");
        // apply note and scheduling updates
        f.Note = dto.Note?.Trim() ?? f.Note;
        f.NextFollowupAt = dto.NextFollowupAt ?? f.NextFollowupAt;
        f.ExpectedResponseAt = dto.ExpectedResponseAt ?? f.ExpectedResponseAt;

        // set server-controlled last followup timestamp
        f.LastFollowupAt = Clock.UtcNowTz;
        f.ModifiedBy = _currentUser.UserName ?? _currentUser.UserId.ToString();
        f.ModifiedDate = Clock.UtcNowTz;

        _repo.Update(f);

        _auditService.AddAudit(
            actionType: "FOLLOWUP_RECORDED",
            module: "Followup",
            entityName: nameof(Followup),
            entityId: f.Id.ToString(),
            oldValues: null,
            newValues: new { f.Id, f.LastFollowupAt, f.Note, f.NextFollowupAt, f.ExpectedResponseAt, Actor = _currentUser.UserName ?? _currentUser.UserId.ToString() },
            description: "Followup action recorded");

        await _context.SaveChangesAsync(ct);
    }
    public async Task<PagedResult<FollowupResponseDto>> GetPagedAsync(FollowupListQueryDto query, CancellationToken ct = default)
    {
        if (query.Page < 1 || query.PageSize < 1) throw new BadRequestException("Page and PageSize must be positive.");
        query.PageSize = Math.Min(query.PageSize, 100);
        if (((long)query.Page - 1) * query.PageSize > int.MaxValue) throw new BadRequestException("Page offset is too large.");
        if (query.BusinessModuleId <= 0 || query.PriorityLevelId <= 0) throw new BadRequestException("Filter IDs must be positive.");
        if (query.Status != null && !string.Equals(query.Status, "Pending", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(query.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Status must be Pending or Completed.");
        if (query.Status != null && query.IsCompleted.HasValue
            && query.IsCompleted.Value != string.Equals(query.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Status and IsCompleted conflict.");
        if (query.DueFrom.HasValue) query.DueFrom = ToUtc(query.DueFrom.Value);
        if (query.DueTo.HasValue) query.DueTo = ToUtc(query.DueTo.Value);
        if (query.DueFrom > query.DueTo) throw new BadRequestException("DueFrom must not exceed DueTo.");
        var page = await _repo.GetPagedAsync(query, Clock.UtcNowTz, ct);
        return new PagedResult<FollowupResponseDto> { Items = await EnrichListAsync(page.Items, ct),
            PageNumber = page.PageNumber, PageSize = page.PageSize, TotalCount = page.TotalCount };
    }

    private static DateTime ToUtc(DateTime value) => value.Kind == DateTimeKind.Unspecified
        ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();

    public async Task<List<FollowupResponseDto>> GetBySourceAsync(long moduleId, string recordId, CancellationToken ct = default)
        => await EnrichListAsync(await _repo.GetBySourceAsync(moduleId, recordId, ct), ct);

    private async Task<List<FollowupResponseDto>> EnrichListAsync(IEnumerable<Followup> items, CancellationToken ct)
    {
        var result = new List<FollowupResponseDto>();
        foreach (var item in items) result.Add(await EnrichAsync(item, ct));
        return result;
    }

    private async Task<FollowupResponseDto> EnrichAsync(Followup f, CancellationToken ct)
    {
        var dto = _mapper.Map<FollowupResponseDto>(f);
        dto.IsCompleted = f.CompletedAt.HasValue;
        dto.IsOverdue = !dto.IsCompleted && f.DueAt != default && f.DueAt < Clock.UtcNowTz;
        var source = await _sourceResolver.ResolveAsync(f.BusinessModuleId, f.BusinessRecordId, ct);
        dto.BusinessModuleCode = source.BusinessModuleCode;
        dto.BusinessModuleName = source.BusinessModuleName;
        dto.BusinessRecordTitle = source.BusinessRecordTitle;
        if (f.PriorityLevelId.HasValue)
            dto.PriorityLevelName = await _context.PriorityLevels.AsNoTracking()
                .Where(p => p.Id == f.PriorityLevelId && !p.IsDeleted).Select(p => p.Name).FirstOrDefaultAsync(ct);
        return dto;
    }
}
