using Jarvis5.Common;
using Jarvis5.Controllers;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Jarvis5.Validators;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Meetings;

public class MeetingAiActionConfirmLifecycleTests(MeetingDelegationDatabase database) : IClassFixture<MeetingDelegationDatabase>
{
    private static MeetingDelegationService Service(EaFmsDbContext db)
    {
        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "ea" && u.UserId == 1);
        var audit = new AuditService(db, user);
        var tasks = new EaTaskService(db, new EaTaskRepository(db), new TatRuleRepository(db),
            new CreateEaTaskDtoValidator(), user, audit);
        var delegations = new DelegationService(db, user, audit, new DelegationRepository(db), tasks,
            Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(),
            new TaskReviewService(db, new TaskReviewRepository(db), user, audit), new TatRuleRepository(db));
        return new(db, delegations, audit);
    }
    private static async Task<Meeting> Seed(EaFmsDbContext db, bool completed = true)
    {
        var meeting = new Meeting { Title = "Delegate flow", CreatedBy = "test", CreatedDate = DateTime.UtcNow,
            CompletedAt = completed ? DateTime.UtcNow : null };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();
        return meeting;
    }
    private static CreateMeetingActionDto Item(long? id = null, string title = "Task") => new()
    {
        MeetingActionId = id, Title = title, Description = "Description", DoerId = " EMP1 ", DoerName = "Doer",
        Priority = "High", DueDate = DateTime.UtcNow.Date.AddDays(3)
    };
    private static ConfirmMeetingAiActionsRequestDto Request(params CreateMeetingActionDto[] items) => new() { Actions = items.ToList() };

    [Fact]
    public async Task MixedItems_UpdatesExisting_CreatesPendingDelegations_AndPreservesFirstDecisionOnReopen()
    {
        await using var db = database.CreateContext();
        var meeting = await Seed(db);
        var existing = MeetingActionFactory.Build(meeting.Id, Item(title: "Old"), "original", DateTime.UtcNow);
        db.MeetingActions.Add(existing);
        db.MeetingActionExtractions.Add(new MeetingActionExtraction { MeetingId = meeting.Id, ProposedActionsJson = "[]", CreatedBy = "test", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var result = await Service(db).ConfirmAsync(meeting.Id, Request(Item(existing.Id, "Updated"), Item(), Item()), "first");
        Assert.Equal(3, result.CreatedDelegationCount);
        Assert.Equal("Delegated", result.DelegationDecision);
        Assert.Equal(existing.Id, result.CreatedActions[0].Id);
        Assert.Equal("Updated", result.CreatedActions[0].Title);
        Assert.Equal(3, await db.MeetingActions.CountAsync(a => a.MeetingId == meeting.Id));
        Assert.All(result.CreatedActions, a => Assert.NotNull(a.DelegationId));
        var delegationIds = result.CreatedActions.Select(a => a.DelegationId!.Value).ToList();
        var delegations = await db.Delegations.Where(d => delegationIds.Contains(d.Id)).ToListAsync();
        Assert.Equal(3, delegations.Count);
        Assert.All(delegations, d => { Assert.Equal("Pending", d.Status); Assert.Equal("EMP1", d.DoerId); Assert.Equal("Doer", d.DoerNameSnapshot); Assert.Equal("High", d.Priority); });
        Assert.True((await db.MeetingActionExtractions.SingleAsync(e => e.MeetingId == meeting.Id)).IsApplied);
        await db.Entry(meeting).ReloadAsync();
        var decidedAt = meeting.DelegationDecidedAt;
        var second = await Service(db).ConfirmAsync(meeting.Id, Request(Item()), "second");
        Assert.Equal(1, second.CreatedDelegationCount);
        Assert.Equal(decidedAt, meeting.DelegationDecidedAt);
        Assert.Equal("first", meeting.DelegationDecidedBy);
        Assert.Equal(4, await db.MeetingActions.CountAsync(a => a.MeetingId == meeting.Id));
    }

    [Fact]
    public async Task ManualConfirmation_NeedsNoAnalysis_AndGetReturnsLinksIncludingHistoricalDelegations()
    {
        await using var db = database.CreateContext();
        var meeting = await Seed(db);
        var result = await Service(db).ConfirmAsync(meeting.Id, Request(Item()), "ea");
        // Historical rows have source links but no decision fields.
        meeting.DelegationDecision = null; meeting.DelegationDecidedAt = null; meeting.DelegationDecidedBy = null;
        var manual = MeetingActionFactory.Build(meeting.Id, Item(), "test", DateTime.UtcNow);
        db.MeetingActions.Add(manual);
        await db.SaveChangesAsync();
        var get = Assert.IsType<OkObjectResult>(await new MeetingsActionsController(db).Get(meeting.Id, default));
        var items = Assert.IsType<List<MeetingActionDto>>(get.Value);
        Assert.Equal(result.CreatedActions[0].DelegationId, items.Single(a => a.Id == result.CreatedActions[0].Id).DelegationId);
        Assert.Null(items.Single(a => a.Id == manual.Id).DelegationId);
        Assert.False(await db.MeetingActionExtractions.AnyAsync(e => e.MeetingId == meeting.Id));
    }

    [Fact]
    public async Task Decline_IsPermanent_AndSecondDeclineAndConfirmConflict()
    {
        await using var db = database.CreateContext();
        var meeting = await Seed(db);
        await Service(db).DeclineAsync(meeting.Id, "ea");
        Assert.Equal("Declined", meeting.DelegationDecision);
        Assert.NotNull(meeting.DelegationDecidedAt);
        Assert.Equal("ea", meeting.DelegationDecidedBy);
        Assert.Equal("Delegation has already been decided for this meeting.",
            (await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).DeclineAsync(meeting.Id, "ea"))).Message);
        Assert.Equal("Delegation was declined for this meeting.",
            (await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).ConfirmAsync(meeting.Id, Request(Item()), "ea"))).Message);
        Assert.False(await db.MeetingActions.AnyAsync(a => a.MeetingId == meeting.Id));
    }

    [Fact]
    public async Task IncompleteMeeting_RejectsBothDecisions()
    {
        await using var db = database.CreateContext();
        var meeting = await Seed(db, false);
        Assert.Equal("Complete the meeting before choosing whether to delegate tasks.",
            (await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).DeclineAsync(meeting.Id, "ea"))).Message);
        Assert.Equal("Complete the meeting before delegating tasks.",
            (await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).ConfirmAsync(meeting.Id, Request(Item()), "ea"))).Message);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("title")]
    [InlineData("doerId")]
    [InlineData("doerName")]
    [InlineData("duplicate")]
    [InlineData("foreign")]
    [InlineData("deleted")]
    public async Task InvalidBatch_SavesNothing(string scenario)
    {
        await using var db = database.CreateContext();
        var meeting = await Seed(db);
        var action = MeetingActionFactory.Build(meeting.Id, Item(), "test", DateTime.UtcNow);
        if (scenario == "foreign") action.MeetingId = (await Seed(db)).Id;
        if (scenario == "deleted") action.IsDeleted = true;
        db.MeetingActions.Add(action);
        await db.SaveChangesAsync();
        var invalid = Item(action.Id);
        if (scenario == "title") invalid.Title = " ";
        if (scenario == "doerId") invalid.DoerId = null;
        if (scenario == "doerName") invalid.DoerName = " ";
        var request = scenario == "empty" ? Request() : scenario == "duplicate" ? Request(invalid, invalid) : Request(Item(), invalid);
        var before = await db.MeetingActions.CountAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).ConfirmAsync(meeting.Id, request, "ea"));
        Assert.Equal(before, await db.MeetingActions.CountAsync());
        Assert.Null((await db.Meetings.FindAsync(meeting.Id))!.DelegationDecision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AlreadyDelegated_RejectsEntireBatch_EvenWhenDelegationDeleted(bool deleted)
    {
        await using var db = database.CreateContext();
        var meeting = await Seed(db);
        var first = await Service(db).ConfirmAsync(meeting.Id, Request(Item()), "ea");
        var delegation = await db.Delegations.FindAsync(first.CreatedActions[0].DelegationId);
        delegation!.IsDeleted = deleted;
        await db.SaveChangesAsync();
        Assert.Equal("Action 'Task' is already delegated.",
            (await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).ConfirmAsync(meeting.Id,
                Request(Item(), Item(first.CreatedActions[0].Id)), "ea"))).Message);
        Assert.Equal(1, await db.MeetingActions.CountAsync(a => a.MeetingId == meeting.Id));
    }

    [Fact]
    public async Task DelegationInsertFailure_RollsBackActionsDecisionAndExtraction()
    {
        await using var db = database.CreateContext();
        var meeting = await Seed(db);
        var action = MeetingActionFactory.Build(meeting.Id, Item(title: "Original"), "test", DateTime.UtcNow);
        db.MeetingActions.Add(action);
        await db.SaveChangesAsync();
        db.MeetingActionExtractions.Add(new MeetingActionExtraction { MeetingId = meeting.Id, ProposedActionsJson = "[]", CreatedBy = "test", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
        // A database constraint rejects the SECOND delegation after the first has been inserted.
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE public.ea_delegations ADD CONSTRAINT meeting_test_failure CHECK (\"Title\" <> 'FAIL_INSERT')");
        try
        {
            var count = await db.Delegations.CountAsync();
            var taskCount = await db.Tasks.CountAsync();
            await Assert.ThrowsAsync<DbUpdateException>(() => Service(db).ConfirmAsync(meeting.Id,
                Request(Item(action.Id, "Updated"), Item(title: "FAIL_INSERT")), "ea"));
            Assert.False((await db.MeetingActionExtractions.SingleAsync(e => e.MeetingId == meeting.Id)).IsApplied);
            Assert.Equal(count, await db.Delegations.CountAsync());
            Assert.Equal(taskCount, await db.Tasks.CountAsync());
            Assert.Single(await db.MeetingActions.Where(a => a.MeetingId == meeting.Id).ToListAsync());
            Assert.Equal("Original", (await db.MeetingActions.FindAsync(action.Id))!.Title);
            Assert.Null((await db.Meetings.FindAsync(meeting.Id))!.DelegationDecision);
        }
        finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE public.ea_delegations DROP CONSTRAINT meeting_test_failure"); }
    }

    [Fact]
    public async Task ConcurrentConfirmation_DelegatesExistingActionOnlyOnce()
    {
        await using var db = database.CreateContext();
        var meeting = await Seed(db);
        var action = MeetingActionFactory.Build(meeting.Id, Item(), "test", DateTime.UtcNow);
        db.MeetingActions.Add(action); await db.SaveChangesAsync();
        async Task<Exception?> Confirm()
        {
            await using var context = database.CreateContext();
            return await Record.ExceptionAsync(() => Service(context).ConfirmAsync(meeting.Id, Request(Item(action.Id)), "ea"));
        }
        var results = await Task.WhenAll(Confirm(), Confirm());
        Assert.Single(results.Where(e => e is null));
        Assert.IsType<BusinessRuleException>(results.Single(e => e is not null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrDeletedMeeting_ReturnsNotFound(bool deleted)
    {
        await using var db = database.CreateContext();
        var meeting = await Seed(db);
        meeting.IsDeleted = deleted; await db.SaveChangesAsync();
        var id = deleted ? meeting.Id : long.MaxValue;
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).DeclineAsync(id, "ea"));
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).ConfirmAsync(id, Request(Item()), "ea"));
    }
    [Fact]
    public async Task DeclineAfterDelegated_IsRejected()
    {
        await using var db = database.CreateContext();
        var meeting = await Seed(db);
        await Service(db).ConfirmAsync(meeting.Id, Request(Item()), "ea");
        Assert.Equal("Delegation has already been decided for this meeting.",
            (await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).DeclineAsync(meeting.Id, "ea"))).Message);
    }

    [Fact]
    public async Task ManualAdd_BeforeAndAfterCompletion_NeverDelegates()
    {
        await using var db = database.CreateContext();
        var meeting = await Seed(db, false);
        var controller = new MeetingsActionsController(db);
        Assert.IsType<CreatedAtActionResult>(await controller.Create(meeting.Id, new CreateMeetingActionDto { Title = "Before" }, default));
        meeting.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Assert.IsType<CreatedAtActionResult>(await controller.Create(meeting.Id, new CreateMeetingActionDto { Title = "After" }, default));
        var actions = await db.MeetingActions.Where(a => a.MeetingId == meeting.Id).ToListAsync();
        Assert.Equal(2, actions.Count);
        Assert.Empty(await MeetingDelegationService.LoadDelegationIdsAsync(db, actions.Select(a => a.Id), default));
    }

    [Fact]
    public async Task DetailAndList_ExposeDecisionFields()
    {
        await using var db = database.CreateContext();
        var meeting = await Seed(db);
        await Service(db).DeclineAsync(meeting.Id, "ea");
        var user = Mock.Of<ICurrentUserService>();
        var service = new MeetingService(new MeetingRepository(db), db,
            Jarvis5.Tests.EaFms.Followups.FollowupBusinessApiTests.Mapper, user,
            Mock.Of<IAuditService>(), Mock.Of<IWorkflowService>(), Mock.Of<IEaTaskService>());
        db.ChangeTracker.Clear();
        var detail = await service.GetByIdAsync(meeting.Id);
        var list = Assert.Single((await service.QueryAsync(pageSize: 1000)).Where(m => m.MeetingId == meeting.Id));
        Assert.Equal("Declined", detail.DelegationDecision);
        Assert.Equal("ea", detail.DelegationDecidedBy);
        Assert.NotNull(detail.DelegationDecidedAt);
        Assert.Equal(detail.DelegationDecision, list.DelegationDecision);
        Assert.Equal(detail.DelegationDecidedBy, list.DelegationDecidedBy);
        Assert.Equal(detail.DelegationDecidedAt, list.DelegationDecidedAt);
    }

    [Fact]
    public async Task Migration_AddsNullableDecisionFields_AndPreservesExistingMeetings()
    {
        await using var db = database.CreateContext();
        var meeting = await Seed(db);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var migration = new Studio5JarvisMasterApi.Migrations.MeetingDelegationDecision();
        var generator = db.GetService<IMigrationsSqlGenerator>();
        foreach (var command in generator.Generate(migration.DownOperations, db.Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        foreach (var command in generator.Generate(migration.UpOperations, db.Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        db.ChangeTracker.Clear();
        var historical = await db.Meetings.SingleAsync(m => m.Id == meeting.Id);
        Assert.Null(historical.DelegationDecision);
        Assert.Null(historical.DelegationDecidedAt);
        Assert.Null(historical.DelegationDecidedBy);
        Assert.Equal(meeting.Title, historical.Title);
        await transaction.RollbackAsync();
    }

}
