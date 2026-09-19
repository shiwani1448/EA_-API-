using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms;

/// <summary>
/// Follow-up &amp; Escalation step 1: the escalation-level catalog must map to the existing
/// public.ea_escalation_levels table (not the convention name "EscalationLevels", which caused
/// PostgreSQL 42P01), and GetActiveLevelsAsync must not wrap failures in AggregateException.
/// </summary>
public class EscalationLevelFoundationTests
{
    private static EaFmsDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static EscalationService MakeService(IEscalationRepository repo, EaFmsDbContext db) =>
        new(repo, db, Mock.Of<IMapper>(), Mock.Of<ICurrentUserService>(), Mock.Of<IAuditService>());

    private static EscalationLevel Level(int id, string code, int level, bool deleted = false) => new()
    {
        Id = id, Code = code, Name = $"Level {code}", Description = $"{code} description",
        Level = level, IsDeleted = deleted, CreatedBy = "seed", CreatedDate = DateTime.UtcNow
    };

    [Fact]
    public void EscalationLevel_IsMappedToExistingEaTable()
    {
        // The relational model needs no live connection to be built.
        using var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options);

        var entity = db.Model.FindEntityType(typeof(EscalationLevel))!;

        Assert.Equal("ea_escalation_levels", entity.GetTableName());
        Assert.Equal("public", entity.GetSchema());
        Assert.Equal("Id", Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
    }

    [Fact]
    public async Task GetActiveLevels_ReturnsOnlyNonDeletedLevelsOrderedByLevel()
    {
        await using var db = MakeDb();
        db.EscalationLevels.AddRange(
            Level(1, "L3", 3), Level(2, "L1", 1), Level(3, "L9", 9, deleted: true), Level(4, "L2", 2));
        await db.SaveChangesAsync();

        var result = await MakeService(new EscalationRepository(db), db).GetActiveLevelsAsync();

        Assert.Equal(new[] { "L1", "L2", "L3" }, result.Select(l => l.Code));
        Assert.Equal(new[] { 1, 2, 3 }, result.Select(l => l.Level));
        Assert.Equal("Level L1", result[0].Name);
        Assert.Equal("L1 description", result[0].Description);
    }

    [Fact]
    public async Task GetActiveLevels_NoConfiguredLevels_ReturnsEmptyList()
    {
        await using var db = MakeDb();

        var result = await MakeService(new EscalationRepository(db), db).GetActiveLevelsAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetActiveLevels_RepositoryFailure_PropagatesOriginalExceptionNotAggregate()
    {
        await using var db = MakeDb();
        var repo = new Mock<IEscalationRepository>();
        repo.Setup(r => r.GetActiveLevelsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleException("boom"));

        var ex = await Record.ExceptionAsync(() => MakeService(repo.Object, db).GetActiveLevelsAsync());

        Assert.IsType<BusinessRuleException>(ex);
        Assert.Equal("boom", ex!.Message);
    }

    [Fact]
    public async Task GetActiveLevels_Cancellation_SurfacesOperationCanceledNotAggregate()
    {
        await using var db = MakeDb();
        var repo = new Mock<IEscalationRepository>();
        repo.Setup(r => r.GetActiveLevelsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var ex = await Record.ExceptionAsync(() => MakeService(repo.Object, db).GetActiveLevelsAsync());

        Assert.IsAssignableFrom<OperationCanceledException>(ex);
    }
}
