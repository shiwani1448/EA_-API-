using System;
using System.Linq;
using System.Threading.Tasks;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Moq;
using Xunit;
using static Jarvis5.Tests.EaFms.Followups.FollowupBusinessApiTests;

namespace Jarvis5.Tests.EaFms.Followups;

public class FollowupEscalationEnrichmentTests
{
    private static FollowupService Service(EaFmsDbContext db) => new(
        new FollowupRepository(db), db, Mapper,
        Mock.Of<ICurrentUserService>(x => x.UserId == 7 && x.UserName == "EA User"),
        Mock.Of<IAuditService>(), new FollowupSourceResolver(db));

    private static async Task<Followup> AddFollowupAsync(EaFmsDbContext db, string subject = "Reminder")
    {
        var followup = new Followup { Subject = subject, DueAt = Base.AddDays(3), CreatedBy = "seed", CreatedDate = Base };
        db.Followups.Add(followup);
        await db.SaveChangesAsync();
        return followup;
    }

    private static async Task<EscalationLevel> AddLevelAsync(EaFmsDbContext db, string name = "Department Head")
    {
        var level = new EscalationLevel { Code = "L1", Name = name, Level = 1, CreatedBy = "seed", CreatedDate = Base };
        db.EscalationLevels.Add(level);
        await db.SaveChangesAsync();
        return level;
    }

    [Fact]
    public async Task DetailAndList_ReturnNullEscalation_WhenFollowupHasNone_WithoutDuplicateRows()
    {
        await using var db = MakeDb();
        var followup = await AddFollowupAsync(db);
        var service = Service(db);

        Assert.Null((await service.GetByIdAsync(followup.Id)).Escalation);
        var page = await service.GetPagedAsync(new FollowupListQueryDto { Page = 1, PageSize = 20 });
        var item = Assert.Single(page.Items);
        Assert.Equal(followup.Id, item.Id);
        Assert.Null(item.Escalation);
    }

    [Fact]
    public async Task Enrichment_ReturnsEscalationAndLevelContext_AndPreservesFilters()
    {
        await using var db = MakeDb();
        var first = await AddFollowupAsync(db, "First");
        var second = await AddFollowupAsync(db, "Second");
        var level = await AddLevelAsync(db, "Director");
        db.Escalations.Add(new Escalation { FollowupId = first.Id, EscalationLevelId = level.Id, InitiatedAt = Base, AcknowledgedAt = Base.AddHours(1), CreatedBy = "seed", CreatedDate = Base });
        await db.SaveChangesAsync();

        var page = await Service(db).GetPagedAsync(new FollowupListQueryDto { IsEscalated = true, EscalationLevelId = level.Id, Page = 1, PageSize = 20 });
        var item = Assert.Single(page.Items);
        Assert.Equal(first.Id, item.Id);
        Assert.NotNull(item.Escalation);
        Assert.Equal((level.Id, "Director", true, false), (item.Escalation!.EscalationLevelId, item.Escalation.EscalationLevelName, item.Escalation.IsAcknowledged, item.Escalation.IsResolved));
        Assert.Equal(Base.AddHours(1), item.Escalation.AcknowledgedAt);
        Assert.Null(item.Escalation.ResolvedAt);
        Assert.DoesNotContain(page.Items, x => x.Id == second.Id);
    }

    [Fact]
    public async Task Enrichment_PrefersLatestUnresolvedEscalation_ThenLatestHistoricalDeterministically()
    {
        await using var db = MakeDb();
        var followup = await AddFollowupAsync(db);
        var level = await AddLevelAsync(db);
        var laterLevel = await AddLevelAsync(db, "Director");
        db.Escalations.AddRange(
            new Escalation { FollowupId = followup.Id, EscalationLevelId = level.Id, InitiatedAt = Base.AddDays(4), ResolvedAt = Base.AddDays(5), CreatedBy = "seed", CreatedDate = Base },
            new Escalation { FollowupId = followup.Id, EscalationLevelId = level.Id, InitiatedAt = Base.AddDays(2), CreatedBy = "seed", CreatedDate = Base },
            new Escalation { FollowupId = followup.Id, EscalationLevelId = laterLevel.Id, InitiatedAt = Base.AddDays(3), CreatedBy = "seed", CreatedDate = Base });
        await db.SaveChangesAsync();

        var current = (await Service(db).GetByIdAsync(followup.Id)).Escalation;
        Assert.NotNull(current);
        Assert.Equal(laterLevel.Id, current!.EscalationLevelId);
        Assert.False(current.IsResolved);

        foreach (var escalation in db.Escalations.Where(x => x.FollowupId == followup.Id && !x.ResolvedAt.HasValue)) escalation.ResolvedAt = Base.AddDays(6);
        await db.SaveChangesAsync();
        var historical = (await Service(db).GetByIdAsync(followup.Id)).Escalation;
        Assert.NotNull(historical);
        Assert.Equal(level.Id, historical!.EscalationLevelId);

        var tiedFirst = new Escalation { FollowupId = followup.Id, EscalationLevelId = level.Id, InitiatedAt = Base.AddDays(7), ResolvedAt = Base.AddDays(8), CreatedBy = "seed", CreatedDate = Base };
        var tiedLast = new Escalation { FollowupId = followup.Id, EscalationLevelId = laterLevel.Id, InitiatedAt = Base.AddDays(7), ResolvedAt = Base.AddDays(8), CreatedBy = "seed", CreatedDate = Base };
        db.Escalations.AddRange(tiedFirst, tiedLast);
        await db.SaveChangesAsync();
        var deterministic = (await Service(db).GetByIdAsync(followup.Id)).Escalation;
        Assert.Equal(tiedLast.Id, deterministic!.Id);
    }

    [Fact]
    public async Task CreateFollowup_RemainsAllowedForCompletedParentTaskContext()
    {
        var seed = await SeedAsync(); await using var _ = seed.Db;
        var module = seed.Modules["Meeting"];
        seed.Db.Tasks.Add(new EaTask { BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = seed.Meeting.Id.ToString(), Task = "Completed task", ExecutionStatus = "Completed", CreatedBy = "seed", CreatedDate = Base });
        await seed.Db.SaveChangesAsync();

        var created = await Service(seed.Db).CreateAsync(new CreateFollowupRequestDto { BusinessModuleId = module.Id, BusinessRecordId = seed.Meeting.Id.ToString(), Subject = "Allowed", DueAt = Base.AddDays(1) });
        Assert.Equal("Completed", created.Stage);
    }
}
