using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jarvis5.Controllers;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Jarvis5.Tests.EaFms.PriorityLevels;

/// <summary>
/// Delegation Next Step 2 — Gap 2: a minimal read-only endpoint over the EXISTING
/// PriorityLevel master (no new table, no write operations). Delegation.Priority and
/// every other existing consumer keep storing the canonical Name string; this endpoint
/// only lets a frontend discover which names are valid.
/// </summary>
public class PriorityLevelsControllerTests
{
    private static EaFmsDbContext MakeDb() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static async Task SeedAsync(EaFmsDbContext db, string name, int level, bool active, bool deleted = false)
    {
        db.PriorityLevels.Add(new PriorityLevel
        {
            Name = name, Level = level, IsActive = active, IsDeleted = deleted,
            CreatedBy = "seed", CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Get_ReturnsExistingPriorityLevels_NoneCreated()
    {
        var db = MakeDb();
        await SeedAsync(db, "Low", 1, active: true);
        await SeedAsync(db, "High", 3, active: true);
        var countBefore = await db.PriorityLevels.CountAsync();
        var controller = new PriorityLevelsController(db);

        var result = await controller.Get(activeOnly: false, ct: default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<List<PriorityLevelDto>>(ok.Value);
        Assert.Equal(2, list.Count);
        Assert.Contains(list, p => p.Name == "Low" && p.Level == 1);
        Assert.Contains(list, p => p.Name == "High" && p.Level == 3);

        // The GET must not have written anything.
        Assert.Equal(countBefore, await db.PriorityLevels.CountAsync());
    }

    [Fact]
    public async Task Get_ActiveOnlyTrue_ExcludesInactiveAndDeleted()
    {
        var db = MakeDb();
        await SeedAsync(db, "Active One", 2, active: true);
        await SeedAsync(db, "Inactive One", 1, active: false);
        await SeedAsync(db, "Deleted One", 4, active: true, deleted: true);
        var controller = new PriorityLevelsController(db);

        var result = await controller.Get(activeOnly: true, ct: default);

        var list = Assert.IsAssignableFrom<List<PriorityLevelDto>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Single(list);
        Assert.Equal("Active One", list[0].Name);
    }

    [Fact]
    public async Task Get_ActiveOnlyFalse_IncludesInactive_ButNeverDeleted()
    {
        var db = MakeDb();
        await SeedAsync(db, "Active", 2, active: true);
        await SeedAsync(db, "Inactive", 1, active: false);
        await SeedAsync(db, "Deleted", 5, active: true, deleted: true);
        var controller = new PriorityLevelsController(db);

        var result = await controller.Get(activeOnly: false, ct: default);

        var list = Assert.IsAssignableFrom<List<PriorityLevelDto>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(2, list.Count);
        Assert.Contains(list, p => p.Name == "Active");
        Assert.Contains(list, p => p.Name == "Inactive");
        Assert.DoesNotContain(list, p => p.Name == "Deleted");
    }

    [Fact]
    public async Task Get_OrdersByLevelDescending_ThenName()
    {
        var db = MakeDb();
        await SeedAsync(db, "Low", 1, active: true);
        await SeedAsync(db, "Critical", 4, active: true);
        await SeedAsync(db, "Medium", 2, active: true);
        var controller = new PriorityLevelsController(db);

        var result = await controller.Get(activeOnly: false, ct: default);
        var list = Assert.IsAssignableFrom<List<PriorityLevelDto>>(Assert.IsType<OkObjectResult>(result).Value);

        Assert.Equal(new[] { "Critical", "Medium", "Low" }, list.Select(p => p.Name));
    }
}
