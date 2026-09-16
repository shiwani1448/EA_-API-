using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Jarvis5.Tests.EaFms.TatRules;

public sealed class ApprovalTatRuleMatchingTests
{
    [Fact]
    public async Task Matches_module_only_when_type_and_subtype_are_absent()
    {
        var (db, repository, module) = await CreateAsync();
        AddRule(db, module, null, null, 60); await db.SaveChangesAsync();

        var match = await repository.GetApplicableForApprovalAsync(module.Id, null, null, default);

        Assert.Single(match); Assert.Equal(60, match[0].TatMinutes);
    }

    [Fact]
    public async Task Matches_module_and_type_when_subtype_is_absent()
    {
        var (db, repository, module) = await CreateAsync();
        AddRule(db, module, "Verification", null, 90); await db.SaveChangesAsync();

        var match = await repository.GetApplicableForApprovalAsync(module.Id, " verification ", null, default);

        Assert.Single(match); Assert.Equal(90, match[0].TatMinutes);
    }

    [Fact]
    public async Task Prefers_exact_module_type_and_subtype_match()
    {
        var (db, repository, module) = await CreateAsync();
        AddRule(db, module, null, null, 60); AddRule(db, module, "Verification", null, 90); AddRule(db, module, "Verification", "Finance", 120); await db.SaveChangesAsync();

        var match = await repository.GetApplicableForApprovalAsync(module.Id, "verification", " finance ", default);

        Assert.Single(match); Assert.Equal(120, match[0].TatMinutes);
    }

    [Fact]
    public async Task Falls_back_from_exact_to_type_then_module()
    {
        var (db, repository, module) = await CreateAsync();
        AddRule(db, module, null, null, 60); AddRule(db, module, "Verification", null, 90); await db.SaveChangesAsync();

        var typeFallback = await repository.GetApplicableForApprovalAsync(module.Id, "VERIFICATION", "Finance", default);
        var moduleFallback = await repository.GetApplicableForApprovalAsync(module.Id, "Other", null, default);

        Assert.Single(typeFallback); Assert.Equal(90, typeFallback[0].TatMinutes);
        Assert.Single(moduleFallback); Assert.Equal(60, moduleFallback[0].TatMinutes);
    }

    [Fact]
    public async Task Ignores_inactive_deleted_and_nonpositive_rules_and_returns_no_match()
    {
        var (db, repository, module) = await CreateAsync();
        AddRule(db, module, "Verification", "Finance", 30, isActive: false);
        AddRule(db, module, "Verification", "Finance", 30, isDeleted: true);
        AddRule(db, module, "Verification", "Finance", 0);
        await db.SaveChangesAsync();

        var match = await repository.GetApplicableForApprovalAsync(module.Id, "verification", "finance", default);

        Assert.Empty(match);
    }

    [Fact]
    public async Task Matching_is_case_insensitive_and_keeps_the_related_module_name_available()
    {
        var (db, repository, module) = await CreateAsync();
        AddRule(db, module, "Verification", "Finance", 45); await db.SaveChangesAsync();

        var match = await repository.GetApplicableForApprovalAsync(module.Id, " VERIFICATION ", " finance ", default);

        Assert.Single(match);
        Assert.Equal(module.Id, match[0].BusinessModuleId);
        Assert.Equal("EA Approval", match[0].ModuleName);
    }

    private static async Task<(EaFmsDbContext Db, TatRuleRepository Repository, BusinessModule Module)> CreateAsync()
    {
        var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        var module = new BusinessModule { Name = "EA Approval", IsActive = true, CreatedBy = "test", CreatedDate = DateTime.UtcNow };
        db.BusinessModules.Add(module); await db.SaveChangesAsync();
        return (db, new TatRuleRepository(db), module);
    }

    private static void AddRule(EaFmsDbContext db, BusinessModule module, string? type, string? subtype, int minutes, bool isActive = true, bool isDeleted = false) =>
        db.TatRules.Add(new TatRule { BusinessModuleId = module.Id, BusinessModule = module, ModuleName = module.Name, Type = type, Subtype = subtype, TatMinutes = minutes, IsActive = isActive, IsDeleted = isDeleted, CreatedBy = "test", CreatedDate = DateTime.UtcNow });
}
