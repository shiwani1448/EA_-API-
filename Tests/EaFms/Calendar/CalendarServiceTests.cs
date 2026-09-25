using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Calendar;

/// <summary>
/// CalendarService: the EA's own entries (ea_calendar_events, full CRUD) — the part that works
/// exactly like Google Calendar. Module entries shown automatically are covered in
/// CalendarLinkedEventsTests.
/// </summary>
public class CalendarServiceTests
{
    private static EaFmsDbContext NewDb() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static CalendarService Service(EaFmsDbContext db, string actor = "ea-actor") =>
        new(db, Mock.Of<ICurrentUserService>(u => u.UserName == actor && u.UserId == 1L));

    private static readonly DateTime WindowStart = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowEnd = new(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);

    private static CreateCalendarEventDto ValidCreate(string title = "Meeting with SCT team",
        string eventType = CalendarEventType.ClientMeeting,
        DateTime? start = null, DateTime? end = null) => new()
    {
        Title = title,
        EventType = eventType,
        StartDateTime = start ?? new DateTime(2026, 10, 10, 14, 30, 0, DateTimeKind.Utc),
        EndDateTime = end ?? new DateTime(2026, 10, 10, 17, 30, 0, DateTimeKind.Utc),
        OrganizerEmployeeId = "emp-1",
        OrganizerName = "Anurag",
    };

    // ============================================================
    // Create
    // ============================================================

    [Fact]
    public async Task CreateEvent_Valid_Persists()
    {
        await using var db = NewDb();
        var result = await Service(db).CreateEventAsync(ValidCreate());

        Assert.True(result.Id > 0);
        Assert.Equal("Meeting with SCT team", result.Title);
        Assert.Equal(CalendarEventType.ClientMeeting, result.EventType);
        Assert.False(result.IsCompleted);
        Assert.Equal(1, await db.CalendarEvents.CountAsync());
    }

    [Fact]
    public async Task CreateEvent_MissingTitle_Throws()
    {
        await using var db = NewDb();
        var dto = ValidCreate(title: "  ");
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).CreateEventAsync(dto));
    }

    [Fact]
    public async Task CreateEvent_MissingStartDateTime_Throws()
    {
        await using var db = NewDb();
        var dto = ValidCreate();
        dto.StartDateTime = null;
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).CreateEventAsync(dto));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NotAType")]
    public async Task CreateEvent_InvalidEventType_Throws(string? eventType)
    {
        await using var db = NewDb();
        var dto = ValidCreate();
        dto.EventType = eventType;
        await Assert.ThrowsAsync<BadRequestException>(() => Service(db).CreateEventAsync(dto));
    }

    // ============================================================
    // Get / Update / Delete
    // ============================================================

    [Fact]
    public async Task GetEvent_Existing_ReturnsDto()
    {
        await using var db = NewDb();
        var created = await Service(db).CreateEventAsync(ValidCreate());

        var fetched = await Service(db).GetEventAsync(created.Id);
        Assert.Equal(created.Id, fetched.Id);
    }

    [Fact]
    public async Task GetEvent_Missing_Throws()
    {
        await using var db = NewDb();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).GetEventAsync(999));
    }

    [Fact]
    public async Task UpdateEvent_Valid_ChangesFields()
    {
        await using var db = NewDb();
        var created = await Service(db).CreateEventAsync(ValidCreate());

        var updated = await Service(db).UpdateEventAsync(created.Id, new UpdateCalendarEventDto
        {
            Title = "Rescheduled meeting",
            EventType = CalendarEventType.InternalMeeting,
            StartDateTime = created.StartDateTime.AddHours(1),
            IsCompleted = true,
        });

        Assert.Equal("Rescheduled meeting", updated.Title);
        Assert.Equal(CalendarEventType.InternalMeeting, updated.EventType);
        Assert.True(updated.IsCompleted);
        Assert.NotNull(updated.ModifiedDate);
    }

    [Fact]
    public async Task UpdateEvent_Missing_Throws()
    {
        await using var db = NewDb();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Service(db).UpdateEventAsync(999, new UpdateCalendarEventDto { Title = "x", StartDateTime = DateTime.UtcNow, EventType = CalendarEventType.Personal }));
    }

    [Fact]
    public async Task DeleteEvent_SoftDeletes_NotReturnedAgain()
    {
        await using var db = NewDb();
        var created = await Service(db).CreateEventAsync(ValidCreate());

        await Service(db).DeleteEventAsync(created.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).GetEventAsync(created.Id));
        var stillInDb = await db.CalendarEvents.IgnoreQueryFilters().SingleAsync(e => e.Id == created.Id);
        Assert.True(stillInDb.IsDeleted);
    }

    [Fact]
    public async Task DeleteEvent_Missing_Throws()
    {
        await using var db = NewDb();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).DeleteEventAsync(999));
    }

    // ============================================================
    // GetEvents — filtering
    // ============================================================

    [Fact]
    public async Task GetEvents_FiltersByDateRange()
    {
        await using var db = NewDb();
        var svc = Service(db);
        await svc.CreateEventAsync(ValidCreate(title: "In range", start: new DateTime(2026, 10, 15, 9, 0, 0, DateTimeKind.Utc)));
        await svc.CreateEventAsync(ValidCreate(title: "Before window", start: new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc)));
        await svc.CreateEventAsync(ValidCreate(title: "After window", start: new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc)));

        var result = await svc.GetEventsAsync(new CalendarEventsQueryDto { From = WindowStart, To = WindowEnd });

        var evt = Assert.Single(result);
        Assert.Equal("In range", evt.Title);
    }

    [Fact]
    public async Task GetEvents_FromAfterTo_Throws()
    {
        await using var db = NewDb();
        await Assert.ThrowsAsync<BadRequestException>(() =>
            Service(db).GetEventsAsync(new CalendarEventsQueryDto { From = WindowEnd, To = WindowStart }));
    }

    [Fact]
    public async Task GetEvents_FiltersByEventType()
    {
        await using var db = NewDb();
        var svc = Service(db);
        await svc.CreateEventAsync(ValidCreate(title: "Client", eventType: CalendarEventType.ClientMeeting));
        await svc.CreateEventAsync(ValidCreate(title: "Personal", eventType: CalendarEventType.Personal));

        var result = await svc.GetEventsAsync(new CalendarEventsQueryDto
        {
            From = WindowStart,
            To = WindowEnd,
            EventTypes = new[] { CalendarEventType.Personal },
        });

        var evt = Assert.Single(result);
        Assert.Equal("Personal", evt.Title);
    }

    [Fact]
    public async Task GetEvents_EventTypeFilter_IsCaseInsensitive()
    {
        await using var db = NewDb();
        var svc = Service(db);
        await svc.CreateEventAsync(ValidCreate(title: "Client", eventType: CalendarEventType.ClientMeeting));

        var result = await svc.GetEventsAsync(new CalendarEventsQueryDto
        {
            From = WindowStart,
            To = WindowEnd,
            EventTypes = new[] { "clientmeeting" },
        });

        Assert.Single(result);
    }

    [Fact]
    public async Task GetEvents_InvalidEventType_Throws()
    {
        await using var db = NewDb();
        await Assert.ThrowsAsync<BadRequestException>(() => Service(db).GetEventsAsync(new CalendarEventsQueryDto
        {
            From = WindowStart,
            To = WindowEnd,
            EventTypes = new[] { "NotAType" },
        }));
    }

    [Fact]
    public async Task GetEvents_ExcludeCompleted_WhenIncludeCompletedFalse()
    {
        await using var db = NewDb();
        var svc = Service(db);
        var done = await svc.CreateEventAsync(ValidCreate(title: "Done"));
        await svc.UpdateEventAsync(done.Id, new UpdateCalendarEventDto
        {
            Title = done.Title, StartDateTime = done.StartDateTime, EventType = done.EventType, IsCompleted = true,
        });
        await svc.CreateEventAsync(ValidCreate(title: "Pending"));

        var result = await svc.GetEventsAsync(new CalendarEventsQueryDto { From = WindowStart, To = WindowEnd, IncludeCompleted = false });

        var evt = Assert.Single(result);
        Assert.Equal("Pending", evt.Title);
    }

    [Fact]
    public async Task GetEvents_FiltersByOrganizerAndPriority()
    {
        await using var db = NewDb();
        var svc = Service(db);
        var dto = ValidCreate(title: "Match");
        dto.Priority = "High";
        await svc.CreateEventAsync(dto);

        var other = ValidCreate(title: "NoMatch");
        other.OrganizerEmployeeId = "emp-2";
        other.Priority = "Low";
        await svc.CreateEventAsync(other);

        var result = await svc.GetEventsAsync(new CalendarEventsQueryDto
        {
            From = WindowStart,
            To = WindowEnd,
            OrganizerEmployeeId = "emp-1",
            Priority = "High",
        });

        var evt = Assert.Single(result);
        Assert.Equal("Match", evt.Title);
    }

    [Fact]
    public async Task GetEvents_OrdersByStartDateTime()
    {
        await using var db = NewDb();
        var svc = Service(db);
        await svc.CreateEventAsync(ValidCreate(title: "Second", start: new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc)));
        await svc.CreateEventAsync(ValidCreate(title: "First", start: new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc)));

        var result = await svc.GetEventsAsync(new CalendarEventsQueryDto { From = WindowStart, To = WindowEnd });

        Assert.Equal(new[] { "First", "Second" }, result.Select(e => e.Title));
    }

    // ============================================================
    // Stats
    // ============================================================

    [Fact]
    public async Task GetStats_CountsTotalCompletedAndHours()
    {
        await using var db = NewDb();
        var svc = Service(db);

        var timed = await svc.CreateEventAsync(ValidCreate(title: "Timed",
            start: new DateTime(2026, 10, 10, 14, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 10, 10, 16, 0, 0, DateTimeKind.Utc)));
        await svc.UpdateEventAsync(timed.Id, new UpdateCalendarEventDto
        {
            Title = timed.Title, StartDateTime = timed.StartDateTime, EndDateTime = timed.EndDateTime,
            EventType = timed.EventType, IsCompleted = true,
        });

        var allDay = ValidCreate(title: "AllDay", start: new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc));
        allDay.IsAllDay = true;
        allDay.EndDateTime = null;
        await svc.CreateEventAsync(allDay);

        var stats = await svc.GetStatsAsync(new CalendarEventsQueryDto { From = WindowStart, To = WindowEnd });

        Assert.Equal(2, stats.Total);
        Assert.Equal(1, stats.Completed);
        Assert.Equal(2.0, stats.ScheduledHours);
    }

    [Fact]
    public async Task GetStats_IgnoresIncludeCompletedFilter_AlwaysCountsBoth()
    {
        await using var db = NewDb();
        var svc = Service(db);
        var done = await svc.CreateEventAsync(ValidCreate(title: "Done"));
        await svc.UpdateEventAsync(done.Id, new UpdateCalendarEventDto
        {
            Title = done.Title, StartDateTime = done.StartDateTime, EventType = done.EventType, IsCompleted = true,
        });
        await svc.CreateEventAsync(ValidCreate(title: "Pending"));

        var stats = await svc.GetStatsAsync(new CalendarEventsQueryDto { From = WindowStart, To = WindowEnd, IncludeCompleted = false });

        Assert.Equal(2, stats.Total);
        Assert.Equal(1, stats.Completed);
    }
}
