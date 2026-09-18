using System.Text.Json;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Travel;

public class TravelArrangementTests
{
    private static EaFmsDbContext Db() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
    private static TravelArrangementService Service(EaFmsDbContext db)
    {
        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "operator" && u.UserId == 42);
        return new(db, user, new AuditService(db, user));
    }
    private static TravelRequest Parent(long id = 1, bool required = true, string state = "Approved", string business = "Upcoming") => new()
    {
        Id = id, EaTaskId = 100 + id, ReferenceNo = $"TRV-{id}", ApprovalRequired = required,
        ApprovalState = state, BusinessState = business, CurrentCycleNo = required ? 1 : 0,
        PickupRequired = true, PickupLocation = "Requested pickup", DropLocation = "Requested drop",
        VehiclePreference = "Requested vehicle", ClientGuestDetails = "Requested guest",
        HospitalityRequirement = "Requested hospitality", MeetingEventPurpose = "Requested purpose",
        NumberOfGuests = 10, SpecialArrangements = "Requested arrangements",
        EstimatedLocalTransportCost = 100, EstimatedHospitalityCost = 200,
        CreatedBy = "ea", CreatedDate = DateTime.UtcNow, SubmittedAt = DateTime.UtcNow
    };

    [Theory]
    [InlineData(true, "Approved")]
    [InlineData(false, "NotRequired")]
    public async Task LocalTransport_CreateEligible_OptionalFieldsAndDefaultStatus(bool required, string state)
    {
        using var db = Db(); db.TravelRequests.Add(Parent(required: required, state: state)); await db.SaveChangesAsync();
        var svc = Service(db);
        Assert.Empty(await svc.ListLocalTransportAsync(1));
        var result = await svc.CreateLocalTransportAsync(1, new());
        Assert.Equal("Requested", result.TransportStatus);
        Assert.Null(result.EstimatedCost); Assert.Null(result.ActualCost); Assert.Null(result.Provider);
        Assert.Equal("TRV-1", result.TravelReferenceNo); Assert.Equal("operator", result.CreatedBy);
        Assert.True(result.LocalTransportId > 0); Assert.Null(result.UpdatedAt);
    }

    [Theory]
    [InlineData(true, "NotSubmitted", "Draft")]
    [InlineData(true, "Pending", "Draft")]
    [InlineData(true, "ChangesRequested", "Draft")]
    [InlineData(true, "Rejected", "Draft")]
    [InlineData(false, "NotRequired", "Draft")]
    [InlineData(true, "Approved", "Draft")]
    public async Task LocalTransport_RejectsInvalidReadiness(bool required, string state, string business)
    {
        using var db = Db(); db.TravelRequests.Add(Parent(required: required, state: state, business: business)); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).CreateLocalTransportAsync(1, new()));
        Assert.Empty(db.TravelLocalTransports); Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public async Task LocalTransport_MultipleRecords_GetListUpdate_PreserveParentAndAudit()
    {
        using var db = Db(); var parent = Parent(); db.TravelRequests.AddRange(parent, Parent(2)); await db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(parent); var svc = Service(db);
        var first = await svc.CreateLocalTransportAsync(1, new());
        var second = await svc.CreateLocalTransportAsync(1, new());
        await svc.CreateLocalTransportAsync(2, new());
        Assert.Equal(new[] { first.LocalTransportId, second.LocalTransportId }, (await svc.ListLocalTransportAsync(1)).Select(x => x.LocalTransportId));
        var updated = await svc.UpdateLocalTransportAsync(first.LocalTransportId, new()
        {
            TransportStatus = "Scheduled", PickupLocation = "Actual pickup", DropLocation = "Actual drop", VehiclePreference = "Sedan", TransportType = "AirportPickup", BookingReference = "ABC",
            EstimatedCost = 123.45m, ActualCost = 120m, Currency = "INR", Notes = "Execution notes",
            Provider = "Provider", ScheduledAt = DateTime.UtcNow
        });
        Assert.Equal("Scheduled", updated.TransportStatus); Assert.Equal(123.45m, updated.EstimatedCost);
        Assert.Equal(120m, updated.ActualCost); Assert.Equal("INR", updated.Currency);
        Assert.Equal("Execution notes", updated.Notes); Assert.Equal("Provider", updated.Provider);
        Assert.NotNull(updated.ScheduledAt); Assert.NotNull(updated.UpdatedAt);
        Assert.Equal("Actual pickup", updated.PickupLocation); Assert.Equal("Actual drop", updated.DropLocation); Assert.Equal("Sedan", updated.VehiclePreference); Assert.Equal("AirportPickup", updated.TransportType); Assert.Equal("ABC", updated.BookingReference);
        db.ChangeTracker.Clear();
        var fetched = await svc.GetLocalTransportAsync(first.LocalTransportId);
        Assert.Equal(JsonSerializer.Serialize(updated), JsonSerializer.Serialize(fetched));
        Assert.Equal(before, JsonSerializer.Serialize(await db.TravelRequests.AsNoTracking().SingleAsync(x => x.Id == 1)));
        Assert.Empty(db.Tasks); Assert.Empty(db.WorkflowInstances); Assert.Empty(db.ApprovalRequests);
        Assert.Empty(db.ApprovalCycles); Assert.Empty(db.TravelRequestCycles); Assert.Empty(db.TravelBookings);
        var audit = await db.AuditLogs.OrderByDescending(a => a.Id).FirstAsync();
        Assert.Equal("TRAVEL_LOCAL_TRANSPORT_UPDATE", audit.ActionType);
        Assert.Equal("operator", audit.ActorName); Assert.Equal(first.LocalTransportId.ToString(), audit.EntityId);
        Assert.Contains("Requested", audit.OldValues!); Assert.Contains("Scheduled", audit.NewValues!);
        Assert.Contains("TRV-1", audit.NewValues!);
        Assert.Equal(3, await db.AuditLogs.CountAsync(a => a.ActionType == "TRAVEL_LOCAL_TRANSPORT_CREATE"));
    }

    [Theory]
    [InlineData("Requested")]
    [InlineData("Scheduled")]
    [InlineData("InProgress")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public async Task LocalTransport_SupportsOperationalStatuses(string status)
    {
        using var db = Db(); db.TravelRequests.Add(Parent()); await db.SaveChangesAsync(); var svc = Service(db);
        var row = await svc.CreateLocalTransportAsync(1, new() { TransportStatus = status });
        Assert.Equal(status, row.TransportStatus);
        var updated = await svc.UpdateLocalTransportAsync(row.LocalTransportId, new() { TransportStatus = status });
        Assert.Equal(status, updated.TransportStatus);
        Assert.Equal(status, (await svc.UpdateLocalTransportAsync(row.LocalTransportId, new())).TransportStatus);
        Assert.Single(await svc.ListLocalTransportAsync(1)); // Cancelled/Completed records remain available.
    }

    [Theory]
    [InlineData("estimated")]
    [InlineData("actual")]
    [InlineData("precision")]
    [InlineData("status")]
    [InlineData("length")]
    public async Task LocalTransport_RejectsInvalidInputOnCreateAndUpdate(string invalid)
    {
        using var db = Db(); db.TravelRequests.Add(Parent()); await db.SaveChangesAsync(); var svc = Service(db);
        var row = await svc.CreateLocalTransportAsync(1, new());
        var dto = new SaveTravelLocalTransportDto { EstimatedCost = invalid == "estimated" ? -1 : null,
            ActualCost = invalid == "actual" ? -1 : invalid == "precision" ? 0.001m : null,
            TransportStatus = invalid == "status" ? "Approved" : null, Currency = invalid == "length" ? new string('X', 11) : null };
        await Assert.ThrowsAsync<BadRequestException>(() => svc.CreateLocalTransportAsync(1, dto));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.UpdateLocalTransportAsync(row.LocalTransportId, dto));
        Assert.Single(db.TravelLocalTransports); Assert.Single(db.AuditLogs);
    }

    [Fact]
    public async Task LocalTransport_CannotReparentOrSupplyAuditIdentity()
    {
        using var db = Db(); db.TravelRequests.AddRange(Parent(), Parent(2)); await db.SaveChangesAsync(); var svc = Service(db);
        var row = await svc.CreateLocalTransportAsync(1, new());
        var dto = JsonSerializer.Deserialize<SaveTravelLocalTransportDto>("{\"travelRequestId\":2,\"createdBy\":\"spoof\",\"isDeleted\":true,\"provider\":\"Updated\"}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var updated = await svc.UpdateLocalTransportAsync(row.LocalTransportId, dto);
        Assert.Equal(1, updated.TravelRequestId); Assert.Equal(row.LocalTransportId, updated.LocalTransportId);
        Assert.Equal("operator", updated.CreatedBy); Assert.Equal("TRV-1", updated.TravelReferenceNo);
        Assert.Equal("Updated", updated.Provider); Assert.Empty(await svc.ListLocalTransportAsync(2));
        var json = JsonSerializer.Serialize(updated, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("eaTaskId", json); Assert.DoesNotContain("workflowInstanceId", json);
    }

    [Fact]
    public async Task LocalTransport_MissingDeletedRowsAndParents_ReturnNotFound()
    {
        using var db = Db(); var svc = Service(db);
        await Assert.ThrowsAsync<NotFoundException>(() => svc.CreateLocalTransportAsync(1, new()));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.ListLocalTransportAsync(1));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetLocalTransportAsync(99));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.UpdateLocalTransportAsync(99, new()));
        var parent = Parent(); db.TravelRequests.Add(parent); await db.SaveChangesAsync();
        var first = await svc.CreateLocalTransportAsync(1, new()); var second = await svc.CreateLocalTransportAsync(1, new());
        (await db.TravelLocalTransports.FindAsync(first.LocalTransportId))!.IsDeleted = true; await db.SaveChangesAsync();
        Assert.Single(await svc.ListLocalTransportAsync(1));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetLocalTransportAsync(first.LocalTransportId));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.UpdateLocalTransportAsync(first.LocalTransportId, new()));
        parent.IsDeleted = true; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => svc.CreateLocalTransportAsync(1, new()));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.ListLocalTransportAsync(1));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetLocalTransportAsync(second.LocalTransportId));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.UpdateLocalTransportAsync(second.LocalTransportId, new()));
    }

    [Theory]
    [InlineData(true, "Approved")]
    [InlineData(false, "NotRequired")]
    public async Task Hospitality_CreateEligible_OptionalFieldsAndDefaultStatus(bool required, string state)
    {
        using var db = Db(); db.TravelRequests.Add(Parent(required: required, state: state)); await db.SaveChangesAsync();
        var svc = Service(db);
        Assert.Empty(await svc.ListHospitalityAsync(1));
        var result = await svc.CreateHospitalityAsync(1, new());
        Assert.Equal("Requested", result.Status);
        Assert.Null(result.EstimatedCost); Assert.Null(result.ActualCost); Assert.Null(result.Provider);
        Assert.Equal("TRV-1", result.TravelReferenceNo); Assert.Equal("operator", result.CreatedBy);
        Assert.True(result.HospitalityId > 0); Assert.Null(result.UpdatedAt);
    }

    [Theory]
    [InlineData(true, "NotSubmitted", "Draft")]
    [InlineData(true, "Pending", "Draft")]
    [InlineData(true, "ChangesRequested", "Draft")]
    [InlineData(true, "Rejected", "Draft")]
    [InlineData(false, "NotRequired", "Draft")]
    [InlineData(true, "Approved", "Draft")]
    public async Task Hospitality_RejectsInvalidReadiness(bool required, string state, string business)
    {
        using var db = Db(); db.TravelRequests.Add(Parent(required: required, state: state, business: business)); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).CreateHospitalityAsync(1, new()));
        Assert.Empty(db.TravelHospitalityArrangements); Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public async Task Hospitality_MultipleRecords_GetListUpdate_PreserveParentAndAudit()
    {
        using var db = Db(); var parent = Parent(); db.TravelRequests.AddRange(parent, Parent(2)); await db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(parent); var svc = Service(db);
        var first = await svc.CreateHospitalityAsync(1, new());
        var second = await svc.CreateHospitalityAsync(1, new());
        await svc.CreateHospitalityAsync(2, new());
        Assert.Equal(new[] { first.HospitalityId, second.HospitalityId }, (await svc.ListHospitalityAsync(1)).Select(x => x.HospitalityId));
        var updated = await svc.UpdateHospitalityAsync(first.HospitalityId, new()
        {
            Status = "Scheduled", ClientGuestDetails = "Actual guest", HospitalityRequirement = "Lunch", MeetingEventPurpose = "Review", NumberOfGuests = 0, SpecialArrangements = "Welcome", Location = "Office",
            EstimatedCost = 123.45m, ActualCost = 120m, Currency = "INR", Notes = "Execution notes",
            Provider = "Provider", ScheduledAt = DateTime.UtcNow
        });
        Assert.Equal("Scheduled", updated.Status); Assert.Equal(123.45m, updated.EstimatedCost);
        Assert.Equal(120m, updated.ActualCost); Assert.Equal("INR", updated.Currency);
        Assert.Equal("Execution notes", updated.Notes); Assert.Equal("Provider", updated.Provider);
        Assert.NotNull(updated.ScheduledAt); Assert.NotNull(updated.UpdatedAt);
        Assert.Equal("Actual guest", updated.ClientGuestDetails); Assert.Equal("Lunch", updated.HospitalityRequirement); Assert.Equal("Review", updated.MeetingEventPurpose); Assert.Equal(0, updated.NumberOfGuests); Assert.Equal("Welcome", updated.SpecialArrangements); Assert.Equal("Office", updated.Location);
        db.ChangeTracker.Clear();
        var fetched = await svc.GetHospitalityAsync(first.HospitalityId);
        Assert.Equal(JsonSerializer.Serialize(updated), JsonSerializer.Serialize(fetched));
        Assert.Equal(before, JsonSerializer.Serialize(await db.TravelRequests.AsNoTracking().SingleAsync(x => x.Id == 1)));
        Assert.Empty(db.Tasks); Assert.Empty(db.WorkflowInstances); Assert.Empty(db.ApprovalRequests);
        Assert.Empty(db.ApprovalCycles); Assert.Empty(db.TravelRequestCycles); Assert.Empty(db.TravelBookings);
        var audit = await db.AuditLogs.OrderByDescending(a => a.Id).FirstAsync();
        Assert.Equal("TRAVEL_HOSPITALITY_UPDATE", audit.ActionType);
        Assert.Equal("operator", audit.ActorName); Assert.Equal(first.HospitalityId.ToString(), audit.EntityId);
        Assert.Contains("Requested", audit.OldValues!); Assert.Contains("Scheduled", audit.NewValues!);
        Assert.Contains("TRV-1", audit.NewValues!);
        Assert.Equal(3, await db.AuditLogs.CountAsync(a => a.ActionType == "TRAVEL_HOSPITALITY_CREATE"));
    }

    [Theory]
    [InlineData("Requested")]
    [InlineData("Scheduled")]
    [InlineData("InProgress")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public async Task Hospitality_SupportsOperationalStatuses(string status)
    {
        using var db = Db(); db.TravelRequests.Add(Parent()); await db.SaveChangesAsync(); var svc = Service(db);
        var row = await svc.CreateHospitalityAsync(1, new() { Status = status });
        Assert.Equal(status, row.Status);
        var updated = await svc.UpdateHospitalityAsync(row.HospitalityId, new() { Status = status });
        Assert.Equal(status, updated.Status);
        Assert.Equal(status, (await svc.UpdateHospitalityAsync(row.HospitalityId, new())).Status);
        Assert.Single(await svc.ListHospitalityAsync(1)); // Cancelled/Completed records remain available.
    }

    [Theory]
    [InlineData("estimated")]
    [InlineData("actual")]
    [InlineData("precision")]
    [InlineData("status")]
    [InlineData("length")]
    public async Task Hospitality_RejectsInvalidInputOnCreateAndUpdate(string invalid)
    {
        using var db = Db(); db.TravelRequests.Add(Parent()); await db.SaveChangesAsync(); var svc = Service(db);
        var row = await svc.CreateHospitalityAsync(1, new());
        var dto = new SaveTravelHospitalityDto { EstimatedCost = invalid == "estimated" ? -1 : null,
            ActualCost = invalid == "actual" ? -1 : invalid == "precision" ? 0.001m : null,
            Status = invalid == "status" ? "Approved" : null, Currency = invalid == "length" ? new string('X', 11) : null };
        await Assert.ThrowsAsync<BadRequestException>(() => svc.CreateHospitalityAsync(1, dto));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.UpdateHospitalityAsync(row.HospitalityId, dto));
        Assert.Single(db.TravelHospitalityArrangements); Assert.Single(db.AuditLogs);
    }

    [Fact]
    public async Task Hospitality_CannotReparentOrSupplyAuditIdentity()
    {
        using var db = Db(); db.TravelRequests.AddRange(Parent(), Parent(2)); await db.SaveChangesAsync(); var svc = Service(db);
        var row = await svc.CreateHospitalityAsync(1, new());
        var dto = JsonSerializer.Deserialize<SaveTravelHospitalityDto>("{\"travelRequestId\":2,\"createdBy\":\"spoof\",\"isDeleted\":true,\"provider\":\"Updated\"}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var updated = await svc.UpdateHospitalityAsync(row.HospitalityId, dto);
        Assert.Equal(1, updated.TravelRequestId); Assert.Equal(row.HospitalityId, updated.HospitalityId);
        Assert.Equal("operator", updated.CreatedBy); Assert.Equal("TRV-1", updated.TravelReferenceNo);
        Assert.Equal("Updated", updated.Provider); Assert.Empty(await svc.ListHospitalityAsync(2));
        var json = JsonSerializer.Serialize(updated, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("eaTaskId", json); Assert.DoesNotContain("workflowInstanceId", json);
    }

    [Fact]
    public async Task Hospitality_MissingDeletedRowsAndParents_ReturnNotFound()
    {
        using var db = Db(); var svc = Service(db);
        await Assert.ThrowsAsync<NotFoundException>(() => svc.CreateHospitalityAsync(1, new()));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.ListHospitalityAsync(1));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetHospitalityAsync(99));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.UpdateHospitalityAsync(99, new()));
        var parent = Parent(); db.TravelRequests.Add(parent); await db.SaveChangesAsync();
        var first = await svc.CreateHospitalityAsync(1, new()); var second = await svc.CreateHospitalityAsync(1, new());
        (await db.TravelHospitalityArrangements.FindAsync(first.HospitalityId))!.IsDeleted = true; await db.SaveChangesAsync();
        Assert.Single(await svc.ListHospitalityAsync(1));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetHospitalityAsync(first.HospitalityId));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.UpdateHospitalityAsync(first.HospitalityId, new()));
        parent.IsDeleted = true; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => svc.CreateHospitalityAsync(1, new()));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.ListHospitalityAsync(1));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetHospitalityAsync(second.HospitalityId));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.UpdateHospitalityAsync(second.HospitalityId, new()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("AirportPickup")]
    [InlineData("StationPickup")]
    [InlineData("LocalTransportation")]
    [InlineData("Other")]
    public async Task LocalTransport_TypesAreOptionalAndSupported(string? type)
    {
        using var db = Db(); db.TravelRequests.Add(Parent()); await db.SaveChangesAsync();
        Assert.Equal(type, (await Service(db).CreateLocalTransportAsync(1, new() { TransportType = type })).TransportType);
    }

    [Fact]
    public async Task TypeAndGuestCountValidation()
    {
        using var db = Db(); db.TravelRequests.Add(Parent()); await db.SaveChangesAsync(); var svc = Service(db);
        await Assert.ThrowsAsync<BadRequestException>(() => svc.CreateLocalTransportAsync(1, new() { TransportType = "Flight" }));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.CreateHospitalityAsync(1, new() { NumberOfGuests = -1 }));
        var hospitality = await svc.CreateHospitalityAsync(1, new() { NumberOfGuests = 0 });
        await Assert.ThrowsAsync<BadRequestException>(() => svc.UpdateHospitalityAsync(hospitality.HospitalityId, new() { NumberOfGuests = -1 }));
        var transport = await svc.CreateLocalTransportAsync(1, new());
        await Assert.ThrowsAsync<BadRequestException>(() => svc.UpdateLocalTransportAsync(transport.LocalTransportId, new() { TransportType = "Flight" }));
    }

    [Fact]
    public void ArrangementSchemaDelta_IsLimitedToIntendedTables_OrEmptyAfterMigration()
    {
        using var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseNpgsql("Host=localhost;Database=model_validation;Username=unused;Password=unused").Options);
        var source = db.GetService<IModelRuntimeInitializer>().Initialize(db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
        var target = db.GetService<IDesignTimeModel>().Model;
        var delta = db.GetService<IMigrationsModelDiffer>().GetDifferences(source.GetRelationalModel(), target.GetRelationalModel());
        var allowed = new[] { "ea_travel_local_transports", "ea_travel_hospitality_arrangements" };
        Assert.All(delta, operation =>
        {
            if (operation is CreateTableOperation table) Assert.Contains(table.Name, allowed);
            else if (operation is CreateIndexOperation index) Assert.Contains(index.Table, allowed);
            else Assert.True(false, $"Unexpected schema change: {operation.GetType().Name}");
        });
        foreach (var type in new[] { typeof(TravelLocalTransport), typeof(TravelHospitality) })
        {
            var entity = target.FindEntityType(type)!;
            Assert.Equal(DeleteBehavior.Restrict, Assert.Single(entity.GetForeignKeys()).DeleteBehavior);
            Assert.Equal(18, entity.FindProperty("EstimatedCost")!.GetPrecision());
            Assert.Equal(2, entity.FindProperty("ActualCost")!.GetScale());
            Assert.Null(entity.FindProperty("EaTaskId")); Assert.Null(entity.FindProperty("TravelBookingId"));
        }
    }
}
