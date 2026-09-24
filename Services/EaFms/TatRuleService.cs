using System.Globalization;
using Jarvis5.Common.EaFms;
using FluentValidation;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Jarvis5.Services.EaFms;

public class TatRuleService(EaFmsDbContext db, ITatRuleRepository repository,
    IValidator<SaveTatRuleDto> validator, ICurrentUserService user, IAuditService audit) : ITatRuleService
{
    public async Task<List<TatRuleDto>> QueryAsync(long? moduleId, string? type, string? subtype, CancellationToken ct)
    {
        var query = repository.Query().Where(x => x.IsActive);
        if (moduleId.HasValue) query = query.Where(x => x.BusinessModuleId == moduleId.Value);
        if (type is not null) { var key = await TatClassification.NormalizeAsync(db, type.Trim(), ct); query = query.Where(x => x.Type != null && TatClassification.TrimForMatch(x.Type).ToLower() == key); }
        if (subtype is not null) { var key = await TatClassification.NormalizeAsync(db, subtype.Trim(), ct); query = query.Where(x => x.Subtype != null && TatClassification.TrimForMatch(x.Subtype).ToLower() == key); }
        return (await query.OrderBy(x => x.Id).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<TatRuleDto> GetAsync(long id, CancellationToken ct) =>
        ToDto(await repository.Query().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException($"TAT rule {id} not found."));

    public async Task<TatRuleDto> SaveAsync(long? id, SaveTatRuleDto dto, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid) throw new BusinessRuleException(string.Join("; ", validation.Errors.Select(x => x.ErrorMessage)));
        var type = dto.Type!.Trim();
        var subtype = string.IsNullOrWhiteSpace(dto.Subtype) ? null : dto.Subtype.Trim();
        var typeKey = await TatClassification.NormalizeAsync(db, type, ct);
        var subtypeKey = subtype is null ? null : await TatClassification.NormalizeAsync(db, subtype, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var module = await db.BusinessModules.FirstOrDefaultAsync(x => x.Id == dto.ModuleId && x.IsActive && !x.IsDeleted, ct)
            ?? throw new BusinessRuleException("Module must exist and be active/non-deleted.");
        if (dto.ModuleName is not null
            && !string.Equals(dto.ModuleName.Trim(), module.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("ModuleName must match the selected ModuleId.");
        var typeOnlyModule = EaTaskService.IsTypeOnlyTatModule(module.Name); // Delegation and Follow-up
        // TaskType is required for Delegation (its rules are always Type + TaskType, no Subtype) and now
        // OPTIONAL for EA Approval (its rules may be plain Type[/Subtype] as before, or Type + TaskType
        // with no Subtype — the same shape Delegation uses — for per-phase Actual/Review/Rework rules).
        // Every other module still forbids TaskType entirely. Format (one of Actual/Review/Rework) was
        // already checked by the validator.
        var taskTypeCapable = typeOnlyModule || string.Equals(module.Name.Trim(), "EA Approval", StringComparison.OrdinalIgnoreCase);
        var taskType = string.IsNullOrWhiteSpace(dto.TaskType) ? null : dto.TaskType.Trim();
        if (taskType is null && typeOnlyModule)
            throw new BadRequestException("TaskType is required for Delegation/Follow-up TAT rules.");
        if (taskType is not null && !taskTypeCapable)
            throw new BadRequestException($"{module.Name} TAT rules do not use TaskType; it must be omitted.");
        // Follow-up has no Review/Rework cycle: TaskType must always be Actual for it.
        if (taskType is not null && string.Equals(module.Name.Trim(), "Follow-up", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(taskType, DelegationTaskType.Actual, StringComparison.Ordinal))
            throw new BadRequestException("Follow-up TAT rules only support TaskType Actual.");
        // A rule uses the Type+TaskType-only shape (no Subtype) either because its module is Delegation
        // (always) or because a TaskType was actually supplied for a taskType-capable module (Approval,
        // optionally). Otherwise the ordinary Type[/Subtype] shape applies unchanged.
        var usesTaskTypeOnlyShape = typeOnlyModule || taskType is not null;
        if (subtype is null && !usesTaskTypeOnlyShape)
            throw new BadRequestException("Subtype is required for this module.");
        if (subtype is not null && usesTaskTypeOnlyShape)
            throw new BadRequestException(taskType is not null
                ? "A TAT rule with TaskType set must omit Subtype (Type + TaskType classification only)."
                : $"{module.Name} TAT rules are classified by Type only; Subtype must be omitted.");
        if (dto.IsActive == true && subtype is not null && await db.TatRules.AnyAsync(x => x.BusinessModuleId == dto.ModuleId
            && x.Type != null && x.Subtype != null && TatClassification.TrimForMatch(x.Type).ToLower() == typeKey && TatClassification.TrimForMatch(x.Subtype).ToLower() == subtypeKey
            && x.IsActive && !x.IsDeleted && (!id.HasValue || x.Id != id.Value), ct))
            throw new BusinessRuleException("An active TAT rule already exists for this module/type/subtype combination.");
        // The normalized Delegation partial unique index protects concurrent saves; this is the friendly pre-check. Classification is now Type + TaskType (e.g. "Follow-up" +
        // "Review" is a distinct rule from "Follow-up" + "Actual"), so the duplicate check matches on both.
        if (dto.IsActive == true && subtype is null && await db.TatRules.AnyAsync(x => x.BusinessModuleId == dto.ModuleId
            && x.Type != null && TatClassification.TrimForMatch(x.Type).ToLower() == typeKey
            && x.TaskType != null && x.TaskType.Trim().ToLower() == taskType!.ToLower()
            && (x.Subtype == null || TatClassification.TrimForMatch(x.Subtype) == "")
            && x.IsActive && !x.IsDeleted && (!id.HasValue || x.Id != id.Value), ct))
            throw new BusinessRuleException("An active TAT rule already exists for this module/type/taskType combination.");

        var actor = EaActorSnapshot.From(dto.EmployeeId, dto.EmployeeName);
        var actorDisplay = actor.DisplayName ?? user.UserId.ToString(CultureInfo.InvariantCulture);
        var now = Clock.UtcNowTz;
        var rule = id.HasValue
            ? await repository.GetForUpdateAsync(id.Value, ct) ?? throw new NotFoundException($"TAT rule {id} not found.")
            : new TatRule { CreatedBy = actorDisplay, CreatedByEmployeeId = actor.EmployeeId, CreatedByEmployeeName = actor.EmployeeName, CreatedDate = now };
        var previous = id.HasValue ? new { rule.BusinessModuleId, rule.Type, rule.Subtype, rule.TaskType, rule.TatMinutes, rule.IsActive } : null;
        rule.BusinessModuleId = dto.ModuleId;
        rule.ModuleName = module.Name;
        rule.Type = type;
        rule.Subtype = subtype;
        rule.TaskType = taskType;
        rule.TatMinutes = dto.TatMinutes;
        rule.IsActive = dto.IsActive!.Value;
        if (id.HasValue)
        {
            rule.ModifiedBy = actorDisplay; rule.ModifiedByEmployeeId = actor.EmployeeId;
            rule.ModifiedByEmployeeName = actor.EmployeeName; rule.ModifiedDate = now;
        }
        else await repository.AddAsync(rule, ct);
        try
        {
            await db.SaveChangesAsync(ct);
            audit.AddAudit(id.HasValue ? "TAT_RULE_UPDATE" : "TAT_RULE_CREATE", "TatRule", nameof(TatRule),
                rule.Id.ToString(CultureInfo.InvariantCulture), previous,
                new { rule.BusinessModuleId, rule.Type, rule.Subtype, rule.TaskType, rule.TatMinutes, rule.IsActive, Actor = actor });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "UX_ea_tat_rules_ActiveClassification" })
        {
            throw new BusinessRuleException("An active TAT rule already exists for this module/type/subtype combination.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "UX_ea_tat_rules_ActiveDelegationClassification" })
        {
            throw new BusinessRuleException("An active TAT rule already exists for this module/type/taskType combination.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "UX_ea_tat_rules_ActiveApprovalTaskTypeClassification" })
        {
            throw new BusinessRuleException("An active TAT rule already exists for this module/type/taskType combination.");
        }
        rule.BusinessModule = module;
        return ToDto(rule);
    }

    private static TatRuleDto ToDto(TatRule rule) => new()
    {
        Id = rule.Id, ModuleId = rule.BusinessModuleId, ModuleName = rule.ModuleName,
        Type = rule.Type, Subtype = rule.Subtype, TaskType = rule.TaskType,
        TatMinutes = rule.TatMinutes, IsActive = rule.IsActive,
        CreatedBy = rule.CreatedBy, CreatedByEmployeeId = rule.CreatedByEmployeeId, CreatedByEmployeeName = rule.CreatedByEmployeeName,
        CreatedDate = rule.CreatedDate,
        ModifiedBy = rule.ModifiedBy, ModifiedByEmployeeId = rule.ModifiedByEmployeeId, ModifiedByEmployeeName = rule.ModifiedByEmployeeName,
        ModifiedDate = rule.ModifiedDate
    };
}
