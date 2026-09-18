using System;
using System.Linq;
using System.Threading.Tasks;
using Jarvis5.Controllers;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Jarvis5.Tests.EaFms.Meetings;

/// <summary>
/// Step 5B-1: MeetingAction gains a nullable, opaque AssignedToId alongside its existing
/// OwnerName display snapshot, so it is structurally ready for a future (not yet
/// implemented) conversion into Delegation. MeetingsActionsController has no service
/// layer of its own (it talks to EaFmsDbContext directly), so these tests exercise the
/// controller directly against EF InMemory — the same pattern the rest of this project
/// uses for its service-layer tests, adapted to this controller's actual shape.
/// </summary>
public class MeetingActionAssignedToIdTests
{
    private static EaFmsDbContext MakeDb() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static async Task<Meeting> SeedMeetingAsync(EaFmsDbContext db)
    {
        var meeting = new Meeting
        {
            Title = "Test Meeting", DoerIds = Array.Empty<string>(), DoerNames = Array.Empty<string>(),
            CreatedBy = "seed", CreatedDate = DateTime.UtcNow, IsDeleted = false
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();
        return meeting;
    }

    [Fact]
    public async Task Create_WithAssignedToId_Persists_AlongsideExistingOwnerName()
    {
        var db = MakeDb();
        var meeting = await SeedMeetingAsync(db);
        var controller = new MeetingsActionsController(db);

        var result = await controller.Create(meeting.Id, new CreateMeetingActionDto
        {
            Title = "Follow up with finance",
            AssignedToId = "emp-42",
            OwnerName = "Finance Lead"
        }, default);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var action = Assert.IsType<MeetingAction>(created.Value);
        Assert.Equal("emp-42", action.AssignedToId);
        Assert.Equal("Finance Lead", action.OwnerName); // existing display snapshot preserved unchanged

        var saved = await db.MeetingActions.SingleAsync(a => a.Id == action.Id);
        Assert.Equal("emp-42", saved.AssignedToId);
        Assert.Equal("Finance Lead", saved.OwnerName);
    }

    [Fact]
    public async Task Create_WithoutAssignedToId_OldStylePayload_StillWorks_AndLeavesItNull()
    {
        var db = MakeDb();
        var meeting = await SeedMeetingAsync(db);
        var controller = new MeetingsActionsController(db);

        // Exactly the shape an existing/older caller would send: no AssignedToId at all.
        var result = await controller.Create(meeting.Id, new CreateMeetingActionDto
        {
            Title = "Send updated deck",
            OwnerName = "Priya (display only)"
        }, default);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var action = Assert.IsType<MeetingAction>(created.Value);
        Assert.Null(action.AssignedToId);
        Assert.Equal("Priya (display only)", action.OwnerName);
    }

    [Fact]
    public async Task Create_AssignedToIdIsNullable_NeverFabricatedFromOwnerName()
    {
        var db = MakeDb();
        var meeting = await SeedMeetingAsync(db);
        var controller = new MeetingsActionsController(db);

        await controller.Create(meeting.Id, new CreateMeetingActionDto { Title = "No owner at all" }, default);

        var saved = await db.MeetingActions.SingleAsync();
        Assert.Null(saved.AssignedToId);
        Assert.Null(saved.OwnerName);
    }

    [Fact]
    public async Task Get_ExposesAssignedToId_AlongsideOwnerName_ForBothStyles()
    {
        var db = MakeDb();
        var meeting = await SeedMeetingAsync(db);
        db.MeetingActions.AddRange(
            new MeetingAction { MeetingId = meeting.Id, Title = "A", AssignedToId = "emp-1", OwnerName = "Alice",
                CreatedBy = "seed", CreatedDate = DateTime.UtcNow },
            new MeetingAction { MeetingId = meeting.Id, Title = "B", OwnerName = "Legacy Bob Only",
                CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var controller = new MeetingsActionsController(db);

        var result = await controller.Get(meeting.Id, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<System.Collections.Generic.List<MeetingActionDto>>(ok.Value);
        var a = Assert.Single(list, x => x.Title == "A");
        Assert.Equal("emp-1", a.AssignedToId);
        Assert.Equal("Alice", a.OwnerName);
        var b = Assert.Single(list, x => x.Title == "B");
        Assert.Null(b.AssignedToId);
        Assert.Equal("Legacy Bob Only", b.OwnerName);
    }

    [Fact]
    public async Task Create_TrimsAssignedToId_AndTreatsWhitespaceAsNull()
    {
        var db = MakeDb();
        var meeting = await SeedMeetingAsync(db);
        var controller = new MeetingsActionsController(db);

        await controller.Create(meeting.Id, new CreateMeetingActionDto { Title = "X", AssignedToId = "  emp-9  " }, default);
        await controller.Create(meeting.Id, new CreateMeetingActionDto { Title = "Y", AssignedToId = "   " }, default);

        var saved = await db.MeetingActions.OrderBy(a => a.Id).ToListAsync();
        Assert.Equal("emp-9", saved[0].AssignedToId);
        Assert.Null(saved[1].AssignedToId);
    }

    // ----------------------------------------------------------------
    // SOURCE IDENTITY (future Meeting -> Delegation mapping — not wired yet)
    // ----------------------------------------------------------------

    [Fact]
    public async Task MultipleMeetingActions_FromTheSameMeeting_RemainIndependentlyIdentifiable()
    {
        var db = MakeDb();
        var meeting = await SeedMeetingAsync(db);
        var controller = new MeetingsActionsController(db);

        var first = Assert.IsType<MeetingAction>(Assert.IsType<CreatedAtActionResult>(
            await controller.Create(meeting.Id, new CreateMeetingActionDto { Title = "Book venue", AssignedToId = "emp-1" }, default)).Value);
        var second = Assert.IsType<MeetingAction>(Assert.IsType<CreatedAtActionResult>(
            await controller.Create(meeting.Id, new CreateMeetingActionDto { Title = "Send invites", AssignedToId = "emp-2" }, default)).Value);

        // Distinct stable identity per action from the same Meeting — the prerequisite
        // for each one to eventually become its own, separately identifiable Delegation
        // (SourceEntityId = MeetingAction.Id), NOT one Delegation per Meeting.
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(meeting.Id, first.MeetingId);
        Assert.Equal(meeting.Id, second.MeetingId);

        var futureSourceEntityIds = await db.MeetingActions
            .Where(a => a.MeetingId == meeting.Id)
            .Select(a => a.Id.ToString())
            .ToListAsync();
        Assert.Equal(2, futureSourceEntityIds.Distinct().Count());
    }

    // ----------------------------------------------------------------
    // PRIORITY (EA-wide frontend-owned-requiredness cleanup)
    //
    // MeetingAction previously required PriorityLevelId (a catalog FK, GreaterThan(0)
    // validated). It is now a plain frontend-owned Priority string, matching Meeting/
    // Delegation's convention: no PriorityLevel existence check, no mandatory rule.
    // ----------------------------------------------------------------

    [Theory]
    [InlineData("High")]
    [InlineData("Urgent")]
    [InlineData("Anything Selected By Frontend")]
    public async Task Create_ArbitraryPriority_AcceptedAndStoredVerbatim_NoCatalogGate(string priority)
    {
        var db = MakeDb();
        var meeting = await SeedMeetingAsync(db);
        var controller = new MeetingsActionsController(db);

        var result = await controller.Create(meeting.Id, new CreateMeetingActionDto { Title = "X", Priority = priority }, default);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var action = Assert.IsType<MeetingAction>(created.Value);
        Assert.Equal(priority, action.Priority);

        var saved = await db.MeetingActions.SingleAsync(a => a.Id == action.Id);
        Assert.Equal(priority, saved.Priority);
    }

    [Fact]
    public async Task Create_WithoutPriority_IsNotMandatory()
    {
        var db = MakeDb();
        var meeting = await SeedMeetingAsync(db);
        var controller = new MeetingsActionsController(db);

        var result = await controller.Create(meeting.Id, new CreateMeetingActionDto { Title = "No priority at all" }, default);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var action = Assert.IsType<MeetingAction>(created.Value);
        Assert.Null(action.Priority);
    }

    [Fact]
    public async Task Create_TrimsPriority_AndTreatsWhitespaceAsNull()
    {
        var db = MakeDb();
        var meeting = await SeedMeetingAsync(db);
        var controller = new MeetingsActionsController(db);

        await controller.Create(meeting.Id, new CreateMeetingActionDto { Title = "X", Priority = "  High  " }, default);
        await controller.Create(meeting.Id, new CreateMeetingActionDto { Title = "Y", Priority = "   " }, default);

        var saved = await db.MeetingActions.OrderBy(a => a.Id).ToListAsync();
        Assert.Equal("High", saved[0].Priority);
        Assert.Null(saved[1].Priority);
    }

    [Fact]
    public async Task Get_ExposesPriority_AsPlainString()
    {
        var db = MakeDb();
        var meeting = await SeedMeetingAsync(db);
        db.MeetingActions.Add(new MeetingAction
        {
            MeetingId = meeting.Id, Title = "A", Priority = "Urgent",
            CreatedBy = "seed", CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var controller = new MeetingsActionsController(db);

        var result = await controller.Get(meeting.Id, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<System.Collections.Generic.List<MeetingActionDto>>(ok.Value);
        var a = Assert.Single(list);
        Assert.Equal("Urgent", a.Priority);
    }
}
