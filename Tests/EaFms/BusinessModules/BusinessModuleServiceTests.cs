using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.BusinessModules;

public sealed class BusinessModuleServiceTests
{
    private static EaFmsDbContext Db(string name) => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(name)
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
        .Options);
    private static BusinessModuleService Service(EaFmsDbContext db) => new(db, new BusinessModuleRepository(db),
        Mock.Of<ICurrentUserService>(x => x.UserName == "admin" && x.UserId == 1), Mock.Of<IAuditService>());

    [Fact]
    public async Task Create_NormalizesName_GeneratesId_AndSetsAuditValues()
    {
        await using var db = Db(nameof(Create_NormalizesName_GeneratesId_AndSetsAuditValues));
        var result = await Service(db).CreateAsync(new SaveBusinessModuleDto { Name = "  Decision  ", IsActive = true }, default);
        var saved = await db.BusinessModules.SingleAsync();
        Assert.True(result.Id > 0); Assert.Equal("Decision", saved.Name); Assert.Equal("admin", saved.CreatedBy); Assert.False(saved.IsDeleted);
    }

    [Fact]
    public async Task Create_RejectsNormalizedDuplicate()
    {
        await using var db = Db(nameof(Create_RejectsNormalizedDuplicate));
        db.BusinessModules.Add(new BusinessModule { Name = "Decision", CreatedBy = "seed", CreatedDate = DateTime.UtcNow }); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).CreateAsync(new SaveBusinessModuleDto { Name = " decision " }, default));
    }

    [Fact]
    public async Task List_ExcludesDeleted_AndActiveOnlyExcludesInactive()
    {
        await using var db = Db(nameof(List_ExcludesDeleted_AndActiveOnlyExcludesInactive));
        db.BusinessModules.AddRange(new BusinessModule { Name = "Active", IsActive = true, CreatedBy = "x" }, new BusinessModule { Name = "Inactive", IsActive = false, CreatedBy = "x" }, new BusinessModule { Name = "Deleted", IsActive = true, IsDeleted = true, CreatedBy = "x" }); await db.SaveChangesAsync();
        Assert.Equal(2, (await Service(db).GetAllAsync(false, default)).Count);
        Assert.Single(await Service(db).GetAllAsync(true, default));
    }

    [Fact]
    public async Task Update_ProtectsCanonicalNameAndActiveState_ButAllowsDescription()
    {
        await using var db = Db(nameof(Update_ProtectsCanonicalNameAndActiveState_ButAllowsDescription));
        var meeting = new BusinessModule { Name = "Meeting", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow }; db.BusinessModules.Add(meeting); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).UpdateAsync(meeting.Id, new SaveBusinessModuleDto { Name = "Meetings", IsActive = true }, default));
        // Deactivation is protected on PUT exactly as on DELETE.
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).UpdateAsync(meeting.Id, new SaveBusinessModuleDto { Name = "Meeting", IsActive = false }, default));
        var result = await Service(db).UpdateAsync(meeting.Id, new SaveBusinessModuleDto { Name = "Meeting", Description = "updated", IsActive = true }, default);
        Assert.True(result.IsActive); Assert.Equal("updated", result.Description);
    }
}
