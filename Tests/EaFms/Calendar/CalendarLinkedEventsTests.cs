using System;
using System.Linq;
using System.Threading.Tasks;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DelegationEntity = Jarvis5.Entities.EaFms.Delegation;

namespace Jarvis5.Tests.EaFms.Calendar;

/// <summary>
/// The EA's module work appears on her calendar automatically, read live (never copied):
/// a Meeting on its date and time, a Delegation from start to due date, an Approval on its
/// required date, Travel from departure to return, a Follow-up at its due time. These entries
/// are read-only and point back to their record; only her own work is shown.
/// </summary>
public class CalendarLinkedEventsTests
{
    private const string Ea = "S5I-1013";
    private const string EaName = "Siddhi Jadhav";
    private static readonly DateTime From = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
    private static DateTime Oct(int day, int hour = 0, int minute = 0) => new(2026, 10, day, hour, minute, 0, DateTimeKind.Utc);

    private static EaFmsDbContext NewDb() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static CalendarService Service(EaFmsDbContext db, string? loginId = null, string? loginName = null) =>
        new(db, Jarvis5.Tests.EaFms.Followups.FollowupTestSupport.User(loginId, loginName));

    private static CalendarEventsQueryDto Query(Action<CalendarEventsQueryDto>? tweak = null)
    {
        var q = new CalendarEventsQueryDto { From = From, To = To, EmployeeId = Ea, EmployeeName = EaName };
        tweak?.Invoke(q);
        return q;
    }

    /// <summary>Siddhi's work in October 2026 — one of each module — plus someone else's meeting.</summary>
    private static async Task<EaFmsDbContext> SeedAsync()
    {
        var db = NewDb();
        db.Meetings.AddRange(
            new Meeting { Id = 1, MeetingNumber = "MTG-1", Title = "Board review", MeetingType = "Client", StartDateTime = Oct(10, 9, 0), EndDateTime = Oct(10, 10, 30),
                Location = "Room 2", OrganizerId = Ea, OrganizerName = EaName, CreatedBy = EaName, CreatedDate = Oct(1) },
            new Meeting { Id = 2, MeetingNumber = "MTG-2", Title = "Someone else's sync", StartDateTime = Oct(11, 9, 0), OrganizerName = "Other EA",
                CreatedBy = "Other EA", CreatedDate = Oct(1) },
            new Meeting { Id = 3, MeetingNumber = "MTG-3", Title = "Out of range", StartDateTime = new DateTime(2026, 12, 1, 9, 0, 0, DateTimeKind.Utc),
                OrganizerId = Ea, CreatedBy = EaName, CreatedDate = Oct(1) });
        db.Delegations.Add(new DelegationEntity { Id = 7, ReferenceNo = "DLG-7", EaTaskId = 70, Title = "Prepare deck", DoerId = "E-9", DoerNameSnapshot = "Riya",
            AssignedById = EaName, AssignedByNameSnapshot = EaName, Status = DelegationStatus.InProgress, StartDate = Oct(12), DueDate = Oct(15),
            Priority = "High", CreatedBy = EaName, CreatedDate = Oct(1) });
        db.ApprovalRequests.Add(new ApprovalRequest { Id = 8, ReferenceNo = "APR-8", EaTaskId = 80, RequestTitle = "Laptop purchase", RequestedBy = EaName,
            WorkflowStatus = "Approved", RequiredApprovalDate = Oct(18), CreatedBy = EaName, CreatedAt = Oct(1) });
        db.TravelRequests.Add(new TravelRequest { Id = 9, ReferenceNo = "TRV-9", EaTaskId = 90, Purpose = "Client visit", FromLocation = "Pune", ToLocation = "Delhi",
            DepartureDate = Oct(20), ReturnDate = Oct(22), BusinessState = "Submitted", CreatedBy = EaName, CreatedDate = Oct(1) });
        db.Followups.Add(new Followup { Id = 11, Subject = "Chase vendor quote", DoerId = Ea, DoerName = EaName, DueAt = Oct(25, 15, 30),
            CreatedBy = EaName, CreatedDate = Oct(1) });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task HerModuleWork_AppearsAutomatically_OnTheRightDateAndTime()
    {
        await using var db = await SeedAsync();

        var events = await Service(db).GetEventsAsync(Query(), default);

        var byKey = events.ToDictionary(e => e.EventKey);
        Assert.Equal(new[] { "Meeting:1", "Delegation:7", "Approval:8", "Travel:9", "Follow-up:11" }, events.Select(e => e.EventKey));

        var meeting = byKey["Meeting:1"];
        Assert.Equal((Oct(10, 9, 0), (DateTime?)Oct(10, 10, 30), false, "Room 2", CalendarEventType.ClientMeeting),
            (meeting.StartDateTime, meeting.EndDateTime, meeting.IsAllDay, meeting.Location, meeting.EventType));

        var delegation = byKey["Delegation:7"];
        Assert.Equal((Oct(12), (DateTime?)Oct(15), true, CalendarEventType.Task, "InProgress", "High"),
            (delegation.StartDateTime, delegation.EndDateTime, delegation.IsAllDay, delegation.EventType, delegation.Status, delegation.Priority));

        var approval = byKey["Approval:8"];
        Assert.Equal((Oct(18), true, true), (approval.StartDateTime, approval.IsAllDay, approval.IsCompleted));   // approved = done

        var travel = byKey["Travel:9"];
        Assert.Equal((Oct(20), (DateTime?)Oct(22), CalendarEventType.Travel, "Delhi"), (travel.StartDateTime, travel.EndDateTime, travel.EventType, travel.Location));
        Assert.Equal("Pune → Delhi", travel.Description);

        var followup = byKey["Follow-up:11"];
        Assert.Equal((Oct(25, 15, 30), false, "Pending"), (followup.StartDateTime, followup.IsAllDay, followup.Status));
    }

    [Fact]
    public async Task ModuleEntries_AreReadOnly_AndPointBackToTheirRecord()
    {
        await using var db = await SeedAsync();

        var events = await Service(db).GetEventsAsync(Query(), default);

        Assert.All(events, e =>
        {
            Assert.True(e.IsReadOnly);
            Assert.Equal(0, e.Id);
            Assert.NotNull(e.SourceRecordId);
            Assert.Equal($"{e.Source}:{e.SourceRecordId}", e.EventKey);
        });
        Assert.Equal("DLG-7", events.Single(e => e.Source == "Delegation").ReferenceNo);
    }

    [Fact]
    public async Task HerOwnEntries_StillWork_AndAreMixedIn_SortedByTime()
    {
        await using var db = await SeedAsync();
        var svc = Service(db);
        await svc.CreateEventAsync(new CreateCalendarEventDto { Title = "Focus time", EventType = CalendarEventType.Personal,
            StartDateTime = Oct(10, 11, 0), EndDateTime = Oct(10, 12, 0), OrganizerEmployeeId = Ea, OrganizerName = EaName }, default);

        var events = await svc.GetEventsAsync(Query(), default);

        var own = events.Single(e => e.Source == "Calendar");
        Assert.False(own.IsReadOnly);
        Assert.True(own.Id > 0);
        Assert.Equal(new[] { "Meeting:1", own.EventKey }, events.Take(2).Select(e => e.EventKey));   // 9:00 meeting, then 11:00 focus time
    }

    [Fact]
    public async Task OnlyHerWork_IsShown_TheLoginIsUsedWhenNoEmployeeIsSent()
    {
        await using var db = await SeedAsync();

        var viaLogin = await Service(db, loginId: Ea, loginName: EaName).GetEventsAsync(new CalendarEventsQueryDto { From = From, To = To }, default);
        Assert.DoesNotContain(viaLogin, e => e.EventKey == "Meeting:2");
        Assert.Equal(5, viaLogin.Count);

        var otherEa = await Service(db).GetEventsAsync(Query(q => { q.EmployeeId = null; q.EmployeeName = "Other EA"; }), default);
        Assert.Equal(new[] { "Meeting:2" }, otherEa.Select(e => e.EventKey));

        var everyone = await Service(db).GetEventsAsync(new CalendarEventsQueryDto { From = From, To = To }, default);
        Assert.Equal(6, everyone.Count);   // no login, no employee: everything in range
    }

    [Fact]
    public async Task AttendedMeetings_AndDelegationsAssignedToHer_AreIncluded()
    {
        await using var db = await SeedAsync();
        db.MeetingAttendees.Add(new MeetingAttendee { MeetingId = 2, ParticipantId = Ea, ParticipantName = EaName, CreatedBy = "seed", CreatedDate = Oct(1) });
        db.Delegations.Add(new DelegationEntity { Id = 12, ReferenceNo = "DLG-12", EaTaskId = 120, Title = "Her own task", DoerId = Ea, DoerNameSnapshot = EaName,
            AssignedById = "Boss", Status = DelegationStatus.Pending, DueDate = Oct(28), CreatedBy = "Boss", CreatedDate = Oct(1) });
        await db.SaveChangesAsync();

        var events = await Service(db).GetEventsAsync(Query(), default);

        Assert.Contains(events, e => e.EventKey == "Meeting:2");
        Assert.Contains(events, e => e.EventKey == "Delegation:12" && e.StartDateTime == Oct(28));   // due date only → shown on the due date
    }

    [Fact]
    public async Task Filters_SourcesEventTypesCompletedAndIncludeLinked()
    {
        await using var db = await SeedAsync();
        var svc = Service(db);

        var meetingsOnly = await svc.GetEventsAsync(Query(q => q.Sources = ["meeting"]), default);
        Assert.Equal(new[] { "Meeting:1" }, meetingsOnly.Select(e => e.EventKey));

        var tasks = await svc.GetEventsAsync(Query(q => q.EventTypes = ["Task"]), default);
        Assert.Equal(new[] { "Delegation:7", "Approval:8", "Follow-up:11" }, tasks.Select(e => e.EventKey));

        var open = await svc.GetEventsAsync(Query(q => q.IncludeCompleted = false), default);
        Assert.DoesNotContain(open, e => e.EventKey == "Approval:8");

        var standalone = await svc.GetEventsAsync(Query(q => q.IncludeLinked = false), default);
        Assert.Empty(standalone);

        await Assert.ThrowsAsync<BadRequestException>(() => svc.GetEventsAsync(Query(q => q.Sources = ["Payroll"]), default));
    }

    [Fact]
    public async Task Stats_CountModuleEntriesToo()
    {
        await using var db = await SeedAsync();

        var stats = await Service(db).GetStatsAsync(Query(), default);

        Assert.Equal((5, 1), (stats.Total, stats.Completed));
        Assert.Equal(1.5, stats.ScheduledHours);   // the 9:00–10:30 meeting is the only timed entry with an end
    }

    [Fact]
    public async Task ChangingTheSourceRecord_MovesTheCalendarEntry_NoSyncNeeded()
    {
        await using var db = await SeedAsync();
        var svc = Service(db);

        var meeting = await db.Meetings.SingleAsync(m => m.Id == 1);
        meeting.StartDateTime = Oct(14, 16, 0);
        meeting.EndDateTime = Oct(14, 17, 0);
        await db.SaveChangesAsync();

        var moved = (await svc.GetEventsAsync(Query(), default)).Single(e => e.EventKey == "Meeting:1");
        Assert.Equal((Oct(14, 16, 0), (DateTime?)Oct(14, 17, 0)), (moved.StartDateTime, moved.EndDateTime));
    }
}
