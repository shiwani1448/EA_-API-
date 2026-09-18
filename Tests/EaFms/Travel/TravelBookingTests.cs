using System.Text.Json;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Travel;

public class TravelBookingTests
{
    private static EaFmsDbContext Db() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
    private static TravelBookingService Service(EaFmsDbContext db)
    {
        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "ea" && u.UserId == 42);
        return new(db, user, new AuditService(db, user));
    }
    private static TravelRequest Parent(long id = 1, bool required = true, string state = "Approved", string business = "Upcoming") => new()
    {
        Id = id, ReferenceNo = $"TRV-{id}", EaTaskId = 100 + id, ApprovalRequired = required,
        ApprovalState = state, BusinessState = business, CreatedBy = "ea", CreatedDate = DateTime.UtcNow,
        TransportType = "Air", Hotel = "Preferred hotel", EstimatedTravelCost = 100,
        EstimatedHotelCost = 200, EstimatedLocalTransportCost = 30, EstimatedHospitalityCost = 40,
        CurrentCycleNo = required ? 1 : 0, SubmittedAt = DateTime.UtcNow
    };
    private static async Task<TravelBookingResponseDto> Create(TravelBookingService svc, long parent = 1, string type = "Flight") =>
        await svc.CreateAsync(parent, new() { BookingType = type });

    [Theory]
    [InlineData("Flight")]
    [InlineData("Train")]
    [InlineData("RoadCar")]
    [InlineData("Hotel")]
    [InlineData("LocalTransport")]
    public async Task Create_ApprovedTravel_AcceptsTypesAndNullableFields_DefaultsToRequested(string type)
    {
        using var db = Db(); db.TravelRequests.Add(Parent()); await db.SaveChangesAsync();
        var result = await Create(Service(db), type: type);
        Assert.Equal(type, result.BookingType);
        Assert.Equal("Requested", result.BookingStatus);
        Assert.Null(result.Cost); Assert.Null(result.Provider); Assert.Null(result.BookingDate);
        Assert.Equal("TRV-1", result.TravelReferenceNo); Assert.Equal("ea", result.CreatedBy);
        Assert.True(result.BookingId > 0);
    }

    [Fact]
    public async Task Create_SubmittedNotRequiredTravel_IsEligible()
    {
        using var db = Db(); db.TravelRequests.Add(Parent(required: false, state: "NotRequired")); await db.SaveChangesAsync();
        Assert.Equal("Requested", (await Create(Service(db))).BookingStatus);
    }

    [Theory]
    [InlineData(true, "NotSubmitted", "Draft")]
    [InlineData(true, "Pending", "Draft")]
    [InlineData(true, "ChangesRequested", "Draft")]
    [InlineData(true, "Rejected", "Draft")]
    [InlineData(false, "NotRequired", "Draft")]
    [InlineData(true, "Approved", "Draft")]
    public async Task Create_RejectsTravelBeforeOperationalApprovalGate(bool required, string state, string business)
    {
        using var db = Db(); db.TravelRequests.Add(Parent(required: required, state: state, business: business)); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Create(Service(db)));
        Assert.Empty(db.TravelBookings); Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public async Task Update_ProgressionAndData_PreserveParentPreferencesEstimatesAndIdentity_AndAudit()
    {
        using var db = Db(); var parent = Parent(); db.TravelRequests.Add(parent); await db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(parent); var svc = Service(db);
        var created = await Create(svc);
        var progress = await svc.UpdateAsync(created.BookingId, new()
        {
            BookingStatus = "InProgress", Provider = "Carrier", BookingReference = "ABC123",
            BookingDate = DateTime.UtcNow, DepartureDetails = "Terminal A", ArrivalDetails = "Terminal B",
            HotelDetails = "Confirmed hotel", VehicleDetails = "Sedan", Cost = 450.25m, Currency = "INR", Notes = "Confirmed"
        });
        Assert.Equal("Carrier", progress.Provider); Assert.Equal("ABC123", progress.BookingReference);
        Assert.Equal(450.25m, progress.Cost); Assert.Equal("INR", progress.Currency);
        Assert.Equal("Terminal A", progress.DepartureDetails); Assert.Equal("Terminal B", progress.ArrivalDetails);
        Assert.Equal("Confirmed hotel", progress.HotelDetails); Assert.Equal("Sedan", progress.VehicleDetails);
        Assert.Equal("Confirmed", progress.Notes); Assert.NotNull(progress.UpdatedAt);
        var booked = await svc.UpdateAsync(created.BookingId, new() { BookingStatus = "Booked" });
        Assert.Equal("Booked", booked.BookingStatus);
        var cancelled = await svc.CancelAsync(created.BookingId);
        Assert.Equal("Cancelled", cancelled.BookingStatus);
        Assert.Equal(before, JsonSerializer.Serialize(await db.TravelRequests.AsNoTracking().SingleAsync()));
        Assert.Empty(db.Tasks); Assert.Empty(db.WorkflowInstances); Assert.Empty(db.ApprovalRequests);
        Assert.Empty(db.ApprovalCycles); Assert.Empty(db.TravelRequestCycles);
        Assert.Equal(new[] { "TRAVEL_BOOKING_CREATE", "TRAVEL_BOOKING_UPDATE", "TRAVEL_BOOKING_UPDATE", "TRAVEL_BOOKING_CANCEL" },
            await db.AuditLogs.OrderBy(a => a.Id).Select(a => a.ActionType).ToArrayAsync());
        var audit = await db.AuditLogs.OrderByDescending(a => a.Id).FirstAsync();
        Assert.Contains("Booked", audit.OldValues!); Assert.Contains("Cancelled", audit.NewValues!);
        Assert.Contains("TRV-1", audit.NewValues!); Assert.Equal("ea", audit.ActorName);
        Assert.Contains($"\"bookingId\":{created.BookingId}", audit.NewValues!);
    }

    [Theory]
    [InlineData("Requested")]
    [InlineData("InProgress")]
    [InlineData("Booked")]
    public async Task Cancel_AllAllowedStates_RetainsBooking_RejectsDuplicate(string state)
    {
        using var db = Db(); db.TravelRequests.Add(Parent()); await db.SaveChangesAsync();
        var svc = Service(db); var created = await Create(svc);
        if (state != "Requested") await svc.UpdateAsync(created.BookingId, new() { BookingStatus = "InProgress" });
        if (state == "Booked") await svc.UpdateAsync(created.BookingId, new() { BookingStatus = "Booked" });
        await svc.CancelAsync(created.BookingId);
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.CancelAsync(created.BookingId));
        Assert.Equal("Cancelled", Assert.Single(await svc.ListAsync(1)).BookingStatus);
        Assert.False((await db.TravelBookings.SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task List_MultipleBookings_IsolatesParent_ExcludesDeleted()
    {
        using var db = Db(); db.TravelRequests.AddRange(Parent(), Parent(2)); await db.SaveChangesAsync();
        var svc = Service(db); var first = await Create(svc); var second = await Create(svc, type: "Hotel");
        await Create(svc, 2); var deleted = await Create(svc);
        (await db.TravelBookings.FindAsync(deleted.BookingId))!.IsDeleted = true; await db.SaveChangesAsync();
        Assert.Equal(new[] { first.BookingId, second.BookingId }, (await svc.ListAsync(1)).Select(b => b.BookingId));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.UpdateAsync(deleted.BookingId, new()));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.CancelAsync(deleted.BookingId));
    }

    [Fact]
    public async Task Update_ParentAndTypeAreNotWritable_OmittedStatusPreservesStatus()
    {
        using var db = Db(); db.TravelRequests.AddRange(Parent(), Parent(2)); await db.SaveChangesAsync();
        var svc = Service(db); var created = await Create(svc);
        var dto = JsonSerializer.Deserialize<UpdateTravelBookingDto>(
            "{\"travelRequestId\":2,\"bookingId\":999,\"bookingType\":\"Hotel\",\"provider\":\"New\"}",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var result = await svc.UpdateAsync(created.BookingId, dto);
        Assert.Equal(1, result.TravelRequestId); Assert.Equal(created.BookingId, result.BookingId);
        Assert.Equal("Flight", result.BookingType); Assert.Equal("Requested", result.BookingStatus);
        Assert.Equal("New", result.Provider);
    }

    [Theory]
    [InlineData("Requested", "Booked")]
    [InlineData("InProgress", "Requested")]
    [InlineData("Booked", "InProgress")]
    [InlineData("Cancelled", "Requested")]
    [InlineData("NotRequired", "InProgress")]
    public async Task Update_RejectsUnsupportedTransitions(string current, string next)
    {
        using var db = Db(); db.TravelRequests.Add(Parent());
        db.TravelBookings.Add(new() { TravelRequestId = 1, BookingType = "Flight", BookingStatus = current });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).UpdateAsync(db.TravelBookings.Single().Id, new() { BookingStatus = next }));
        Assert.Equal(current, db.TravelBookings.Single().BookingStatus);
    }

    [Fact]
    public async Task NotRequired_IsExplicitAndTerminal_CancellationViaUpdateIsAudited()
    {
        using var db = Db(); db.TravelRequests.Add(Parent()); await db.SaveChangesAsync(); var svc = Service(db);
        var notRequired = await svc.CreateAsync(1, new() { BookingType = "Hotel", BookingStatus = "NotRequired" });
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.CancelAsync(notRequired.BookingId));
        var requested = await Create(svc);
        await svc.UpdateAsync(requested.BookingId, new() { BookingStatus = "NotRequired" });
        var another = await Create(svc);
        await svc.UpdateAsync(another.BookingId, new() { BookingStatus = "Cancelled" });
        Assert.Equal("TRAVEL_BOOKING_CANCEL", db.AuditLogs.OrderByDescending(a => a.Id).First().ActionType);
    }

    [Theory]
    [InlineData("type")]
    [InlineData("status")]
    [InlineData("negative")]
    [InlineData("precision")]
    [InlineData("length")]
    public async Task InvalidInput_IsRejectedWithoutWriting(string invalid)
    {
        using var db = Db(); db.TravelRequests.Add(Parent()); await db.SaveChangesAsync();
        var dto = new CreateTravelBookingDto { BookingType = invalid == "type" ? "Hospitality" : "Flight",
            BookingStatus = invalid == "status" ? "Unknown" : null,
            Cost = invalid == "negative" ? -1 : invalid == "precision" ? 1.001m : null,
            Currency = invalid == "length" ? new string('X', 11) : null };
        await Assert.ThrowsAsync<BadRequestException>(() => Service(db).CreateAsync(1, dto));
        Assert.Empty(db.TravelBookings); Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public async Task MissingAndDeletedParentsAndBookings_AreNotFound()
    {
        using var db = Db(); var svc = Service(db);
        await Assert.ThrowsAsync<NotFoundException>(() => Create(svc));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.ListAsync(1));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.UpdateAsync(99, new()));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.CancelAsync(99));
        var parent = Parent(); db.TravelRequests.Add(parent); await db.SaveChangesAsync();
        var booking = await Create(svc); parent.IsDeleted = true; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => Create(svc));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.ListAsync(1));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.UpdateAsync(booking.BookingId, new()));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.CancelAsync(booking.BookingId));
    }

    [Fact]
    public void ModelDelta_IsOnlyBookingBeforeMigration_OrEmptyAfterMigration()
    {
        using var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseNpgsql("Host=localhost;Database=model_validation;Username=unused;Password=unused").Options);
        var source = db.GetService<IModelRuntimeInitializer>().Initialize(
            db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
        var target = db.GetService<IDesignTimeModel>().Model;
        var changes = db.GetService<IMigrationsModelDiffer>().GetDifferences(source.GetRelationalModel(), target.GetRelationalModel());
        Assert.All(changes, operation =>
        {
            if (operation is CreateTableOperation table) Assert.Equal("ea_travel_bookings", table.Name);
            else if (operation is CreateIndexOperation index) Assert.Equal("ea_travel_bookings", index.Table);
            else Assert.True(false, $"Unexpected schema change: {operation.GetType().Name}");
        });
        var booking = target.FindEntityType(typeof(TravelBooking))!;
        Assert.Equal(DeleteBehavior.Restrict, Assert.Single(booking.GetForeignKeys()).DeleteBehavior);
        Assert.Equal(18, booking.FindProperty(nameof(TravelBooking.Cost))!.GetPrecision());
        Assert.Equal(2, booking.FindProperty(nameof(TravelBooking.Cost))!.GetScale());
    }
}
