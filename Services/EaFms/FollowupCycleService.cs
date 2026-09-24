using System.Data;
using System.Globalization;
using FluentValidation;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public class FollowupCycleService : IFollowupCycleService
{
    private readonly EaFmsDbContext _context;
    private readonly IFollowupCycleRepository _repo;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _user;
    private readonly IValidator<CreateFollowupCycleRequestDto> _validator;

    public FollowupCycleService(EaFmsDbContext context, IFollowupCycleRepository repo,
        IAuditService audit, ICurrentUserService user, IValidator<CreateFollowupCycleRequestDto> validator)
    {
        _context = context;
        _repo = repo;
        _audit = audit;
        _user = user;
        _validator = validator;
    }

    public async Task<FollowupCycleResponseDto> CreateAsync(long followupId, CreateFollowupCycleRequestDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid) throw new BadRequestException(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var parent = await _repo.LockParentAsync(followupId, ct)
            ?? throw new NotFoundException($"Followup {followupId} not found.");
        if (parent.CompletedAt.HasValue) throw new BusinessRuleException("Cannot create a cycle for a completed follow-up.");
        var maximum = await _repo.GetMaximumSequenceAsync(followupId, ct);
        if (maximum == int.MaxValue) throw new BadRequestException("Follow-up cycle sequence limit reached.");
        var actor = EaActorSnapshot.From(dto.EmployeeId, dto.EmployeeName);
        var now = Clock.UtcNowTz;
        var cycle = new FollowupCycle
        {
            FollowupId = followupId,
            SequenceNumber = maximum + 1,
            FollowedUpAt = now,
            Note = dto.Note?.Trim(),
            NextFollowupAt = ToUtc(dto.NextFollowupAt),
            ExpectedResponseAt = ToUtc(dto.ExpectedResponseAt),
            OutcomeCode = dto.OutcomeCode?.Trim(),
            FollowedUpByEmployeeId = actor.EmployeeId,
            FollowedUpByEmployeeName = actor.EmployeeName,
            CreatedBy = actor.DisplayName ?? _user.ActorDisplay(),
            CreatedDate = now
        };
        await _repo.AddAsync(cycle, ct);
        await _context.SaveChangesAsync(ct);
        var response = ToResponse(cycle);
        _audit.AddAudit("FOLLOWUP_CYCLE_CREATE", "Followup", nameof(FollowupCycle),
            cycle.Id.ToString(CultureInfo.InvariantCulture), null, response, "Followup cycle created");
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<List<FollowupCycleResponseDto>> GetHistoryAsync(long followupId, CancellationToken ct = default)
    {
        if (!await _repo.ParentExistsAsync(followupId, ct)) throw new NotFoundException($"Followup {followupId} not found.");
        return (await _repo.GetHistoryAsync(followupId, ct)).Select(ToResponse).ToList();
    }

    public async Task<FollowupCycleResponseDto> GetAsync(long followupId, long cycleId, CancellationToken ct = default)
    {
        var cycle = await _repo.GetAsync(followupId, cycleId, ct)
            ?? throw new NotFoundException($"Cycle {cycleId} for followup {followupId} not found.");
        return ToResponse(cycle);
    }

    private static DateTime? ToUtc(DateTime? value) => value.HasValue
        ? value.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value.Value.ToUniversalTime()
        : null;

    private static FollowupCycleResponseDto ToResponse(FollowupCycle cycle) => new()
    {
        Id = cycle.Id, FollowupId = cycle.FollowupId, SequenceNumber = cycle.SequenceNumber,
        FollowedUpByEmployeeId = cycle.FollowedUpByEmployeeId, FollowedUpByEmployeeName = cycle.FollowedUpByEmployeeName,
        FollowedUpAt = cycle.FollowedUpAt, Note = cycle.Note, NextFollowupAt = cycle.NextFollowupAt,
        ExpectedResponseAt = cycle.ExpectedResponseAt, OutcomeCode = cycle.OutcomeCode,
        CreatedBy = cycle.CreatedBy, CreatedDate = cycle.CreatedDate
    };
}
