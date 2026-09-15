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
        var subtype = dto.Subtype!.Trim();
        var typeKey = await TatClassification.NormalizeAsync(db, type, ct);
        var subtypeKey = await TatClassification.NormalizeAsync(db, subtype, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var module = await db.BusinessModules.FirstOrDefaultAsync(x => x.Id == dto.ModuleId && x.IsActive && !x.IsDeleted, ct)
            ?? throw new BusinessRuleException("Module must exist and be active/non-deleted.");
        if (dto.IsActive == true && await db.TatRules.AnyAsync(x => x.BusinessModuleId == dto.ModuleId
            && x.Type != null && x.Subtype != null && TatClassification.TrimForMatch(x.Type).ToLower() == typeKey && TatClassification.TrimForMatch(x.Subtype).ToLower() == subtypeKey
            && x.IsActive && !x.IsDeleted && (!id.HasValue || x.Id != id.Value), ct))
            throw new BusinessRuleException("An active TAT rule already exists for this module/type/subtype combination.");

        var actor = user.UserId.ToString(CultureInfo.InvariantCulture);
        var now = Clock.UtcNowTz;
        var rule = id.HasValue
            ? await repository.GetForUpdateAsync(id.Value, ct) ?? throw new NotFoundException($"TAT rule {id} not found.")
            : new TatRule { CreatedBy = actor, CreatedDate = now };
        var previous = id.HasValue ? new { rule.BusinessModuleId, rule.Type, rule.Subtype, rule.TatMinutes, rule.IsActive } : null;
        rule.BusinessModuleId = dto.ModuleId;
        rule.Type = type;
        rule.Subtype = subtype;
        rule.TatMinutes = dto.TatMinutes;
        rule.IsActive = dto.IsActive!.Value;
        if (id.HasValue) { rule.ModifiedBy = actor; rule.ModifiedDate = now; }
        else await repository.AddAsync(rule, ct);
        try
        {
            await db.SaveChangesAsync(ct);
            audit.AddAudit(id.HasValue ? "TAT_RULE_UPDATE" : "TAT_RULE_CREATE", "TatRule", nameof(TatRule),
                rule.Id.ToString(CultureInfo.InvariantCulture), previous,
                new { rule.BusinessModuleId, rule.Type, rule.Subtype, rule.TatMinutes, rule.IsActive });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "UX_ea_tat_rules_ActiveClassification" })
        {
            throw new BusinessRuleException("An active TAT rule already exists for this module/type/subtype combination.");
        }
        rule.BusinessModule = module;
        return ToDto(rule);
    }

    private static TatRuleDto ToDto(TatRule rule) => new()
    {
        Id = rule.Id, ModuleId = rule.BusinessModuleId, ModuleName = rule.BusinessModule.Name,
        Type = rule.Type, Subtype = rule.Subtype,
        TatMinutes = rule.TatMinutes, IsActive = rule.IsActive,
        CreatedDate = rule.CreatedDate, ModifiedDate = rule.ModifiedDate
    };
}
