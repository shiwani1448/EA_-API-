using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories.EaFms;

public class TatRuleRepository(EaFmsDbContext db) : ITatRuleRepository
{
    public IQueryable<TatRule> Query() => db.TatRules.AsNoTracking()
        .Include(x => x.BusinessModule).Where(x => !x.IsDeleted);

    public Task<TatRule?> GetForUpdateAsync(long id, CancellationToken ct) =>
        db.TatRules.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);

    // Caller holds a transaction. Prevent configuration edits during snapshot creation.
    // Materialize up to two rows: ambiguity must never silently select the first rule.
    public Task<List<TatRule>> GetApplicableAsync(long moduleId, string type, string subtype, CancellationToken ct) =>
        db.TatRules.FromSqlInterpolated($"SELECT * FROM public.ea_tat_rules WHERE \"BusinessModuleId\" = {moduleId} AND lower(btrim(\"Type\")) = lower(btrim({type})) AND lower(btrim(\"Subtype\")) = lower(btrim({subtype})) AND \"IsActive\" AND NOT \"IsDeleted\" LIMIT 2 FOR SHARE")
            .AsNoTracking().ToListAsync(ct);

    // Delegation has a single classification (delegationType): module + Type, with NO subtype. Same normalization as the
    // exact lookup (lower(btrim)), same locking and LIMIT 2 ambiguity detection, and deliberately no fallback to
    // module-level or typed+subtyped rules, so an unconfigured type fails instead of matching something else.
    public Task<List<TatRule>> GetApplicableByTypeOnlyAsync(long moduleId, string type, CancellationToken ct) =>
        db.TatRules.FromSqlInterpolated($"SELECT * FROM public.ea_tat_rules WHERE \"BusinessModuleId\" = {moduleId} AND \"Type\" IS NOT NULL AND lower(btrim(\"Type\")) = lower(btrim({type})) AND (\"Subtype\" IS NULL OR btrim(\"Subtype\") = '') AND \"IsActive\" AND NOT \"IsDeleted\" LIMIT 2 FOR SHARE")
            .AsNoTracking().ToListAsync(ct);

    // Approval supports progressively broader classifications. A type-level rule is
    // represented by a null Subtype; a module-level rule has null Type and Subtype.
    public async Task<List<TatRule>> GetApplicableForApprovalAsync(long moduleId, string? type, string? subtype, CancellationToken ct)
    {
        var typeKey = string.IsNullOrWhiteSpace(type) ? null : type.Trim().ToLowerInvariant();
        var subtypeKey = string.IsNullOrWhiteSpace(subtype) ? null : subtype.Trim().ToLowerInvariant();

        if (typeKey is not null && subtypeKey is not null)
        {
            var exact = await MatchingRules(moduleId, x => x.Type != null && x.Subtype != null
                && x.Type.Trim().ToLower() == typeKey && x.Subtype.Trim().ToLower() == subtypeKey, ct);
            if (exact.Count != 0) return exact;
        }
        if (typeKey is not null)
        {
            var typeOnly = await MatchingRules(moduleId, x => x.Type != null && x.Subtype == null
                && x.Type.Trim().ToLower() == typeKey, ct);
            if (typeOnly.Count != 0) return typeOnly;
        }
        return await MatchingRules(moduleId, x => x.Type == null && x.Subtype == null, ct);
    }

    private Task<List<TatRule>> MatchingRules(long moduleId, System.Linq.Expressions.Expression<Func<TatRule, bool>> classification, CancellationToken ct) =>
        db.TatRules.AsNoTracking().Where(x => x.BusinessModuleId == moduleId && x.IsActive && !x.IsDeleted && x.TatMinutes > 0)
            .Where(classification).Take(2).ToListAsync(ct);

    public async Task AddAsync(TatRule rule, CancellationToken ct) => await db.TatRules.AddAsync(rule, ct);
}
