using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.TatRules;

public sealed class TatRuleReadTests
{
    [Fact]
    public async Task TAT_reads_resolve_module_name_from_the_module_relationship()
    {
        await using var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(nameof(TAT_reads_resolve_module_name_from_the_module_relationship))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        var module = new BusinessModule { Name = "Decision", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.BusinessModules.Add(module); await db.SaveChangesAsync();
        db.TatRules.Add(new TatRule { BusinessModuleId = module.Id, BusinessModule = module, ModuleName = module.Name, Type = "Decision", Subtype = "Standard", TatMinutes = 30, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var service = new TatRuleService(db, new TatRuleRepository(db), Mock.Of<FluentValidation.IValidator<Jarvis5.Dtos.EaFms.SaveTatRuleDto>>(), Mock.Of<ICurrentUserService>(), Mock.Of<IAuditService>());

        var list = await service.QueryAsync(null, null, null, default);
        var detail = await service.GetAsync(list[0].Id, default);
        Assert.Equal(module.Id, detail.ModuleId);
        Assert.Equal("Decision", detail.ModuleName);

        var user = Mock.Of<ICurrentUserService>(x => x.UserName == "admin" && x.UserId == 1);
        var moduleService = new BusinessModuleService(db, new BusinessModuleRepository(db), user, Mock.Of<IAuditService>());
        await moduleService.UpdateAsync(module.Id, new Jarvis5.Dtos.EaFms.SaveBusinessModuleDto { Name = "Decision Management", IsActive = true }, default);
        Assert.Equal("Decision Management", (await service.GetAsync(detail.Id, default)).ModuleName);
    }
}
