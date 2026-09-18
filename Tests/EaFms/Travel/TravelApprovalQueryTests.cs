using System.Text.Json;
using Jarvis5.Common;
using Jarvis5.Controllers.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Travel;

public class TravelApprovalQueryTests
{
    private static EaFmsDbContext Db() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static TravelApprovalQueryService Service(EaFmsDbContext db)
    {
        var audit = Mock.Of<IAuditService>();
        var user = Mock.Of<ICurrentUserService>();
        var requests = new TravelRequestService(db, audit, user,
            Mock.Of<ITravelNumberRepository>(), Mock.Of<IEaTaskService>());
        return new(db, requests, new TravelDocumentService(db, user, audit, Mock.Of<IWebHostEnvironment>()));
    }

    private static TravelRequest Request(long id = 1, string state = "Pending") => new()
    {
        Id = id, ReferenceNo = $"TRV-2026-{id:D6}", EaTaskId = 100 + id,
        BusinessState = "Draft", ApprovalState = state, CurrentCycleNo = 1,
        ApprovalRequired = true, TravellerName = "Ada", Department = "Engineering",
        Purpose = "Client visit", FromLocation = "Pune", ToLocation = "Delhi",
        DepartureDate = new DateTime(2026, 10, 1), ReturnDate = new DateTime(2026, 10, 3),
        RequiredDate = new DateTime(2026, 9, 29), Priority = "High", Currency = "INR",
        EstimatedTravelCost = 100, EstimatedHotelCost = 200, EstimatedHospitalityCost = 50,
        ApproverId = "opaque-director", ApproverNameSnapshot = "Stored Director",
        CreatedBy = "ea", CreatedDate = DateTime.UtcNow, SubmittedAt = DateTime.UtcNow,
        TransportType = "Air", Hotel = "Hotel A", PickupRequired = true,
        HospitalityRequirement = "Lunch", ItineraryNotes = "Arrive early"
    };

    private static TravelRequestCycle Cycle(long requestId, int no, string state = "Pending") => new()
    {
        TravelRequestId = requestId, CycleNo = no, DecisionState = state,
        ApproverId = "opaque-director", ApproverNameSnapshot = "Stored Director",
        SubmittedBy = "ea", SubmittedAt = new DateTime(2026, 9, 17).AddDays(no),
        CreatedBy = "ea", CreatedDate = DateTime.UtcNow
    };

    [Fact]
    public async Task Pending_ReturnsCurrentPending_WithCostsAndOnlyActiveTravelDocumentCount()
    {
        using var db = Db();
        db.TravelRequests.Add(Request());
        db.TravelRequestCycles.Add(Cycle(1, 1));
        foreach (var (module, entity, parent, active, deleted) in new[]
        {
            ("Travel", "TravelRequest", "1", true, false),
            ("Travel", "TravelRequest", "1", true, false),
            ("Travel", "TravelRequest", "1", false, false),
            ("Travel", "TravelRequest", "1", true, true),
            ("Meeting", "TravelRequest", "1", true, false),
            ("Travel", "Meeting", "1", true, false),
            ("Travel", "TravelRequest", "2", true, false)
        }) db.Attachments.Add(new Attachment { RelatedModule = module, RelatedEntity = entity,
            RelatedEntityId = parent, IsActive = active, IsDeleted = deleted });
        await db.SaveChangesAsync();
        var result = await Service(db).PendingAsync(new());
        var item = Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, item.TravelRequestId);
        Assert.Equal("TRV-2026-000001", item.ReferenceNo);
        Assert.Equal("Ada", item.TravellerName);
        Assert.Equal("Engineering", item.Department);
        Assert.Equal("Client visit", item.Purpose);
        Assert.Equal("Pune", item.FromLocation);
        Assert.Equal("Delhi", item.ToLocation);
        Assert.Equal(new DateTime(2026, 9, 29), item.RequiredDate);
        Assert.Equal("High", item.Priority);
        Assert.Equal(350m, item.TotalEstimatedCost);
        Assert.Equal("INR", item.Currency);
        Assert.Equal("opaque-director", item.ApproverId);
        Assert.Equal("Stored Director", item.ApproverName);
        Assert.Equal(2, item.DocumentCount);
        Assert.Equal("Pending", item.ApprovalState);
        Assert.Equal(1, item.CurrentCycleNo);
    }

    [Theory]
    [InlineData("NotRequired")]
    [InlineData("NotSubmitted")]
    [InlineData("ChangesRequested")]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    public async Task Pending_ExcludesOtherRequestStates_EvenWithPendingCycle(string state)
    {
        using var db = Db();
        db.TravelRequests.Add(Request(state: state));
        db.TravelRequestCycles.Add(Cycle(1, 1));
        await db.SaveChangesAsync();
        Assert.Empty((await Service(db).PendingAsync(new())).Items);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("historical-only")]
    [InlineData("decided")]
    [InlineData("deleted")]
    public async Task Pending_RequiresLiveRequestAndMatchingPendingCurrentCycle(string scenario)
    {
        using var db = Db();
        var request = Request();
        db.TravelRequests.Add(request);
        if (scenario != "missing") db.TravelRequestCycles.Add(Cycle(1, 1, scenario == "decided" ? "Approved" : "Pending"));
        if (scenario == "historical-only") request.CurrentCycleNo = 2;
        if (scenario == "deleted") request.IsDeleted = true;
        await db.SaveChangesAsync();
        Assert.Empty((await Service(db).PendingAsync(new())).Items);
    }

    [Fact]
    public async Task Pending_SearchPagingAndOpaqueApproverNames_FollowExistingConvention()
    {
        using var db = Db();
        for (var id = 1; id <= 3; id++)
        {
            db.TravelRequests.Add(Request(id));
            var cycle = Cycle(id, 1);
            cycle.ApproverId = id.ToString(); // Numeric-looking IDs are not reinterpreted as HRMS IDs.
            cycle.ApproverNameSnapshot = null;
            db.TravelRequestCycles.Add(cycle);
        }
        await db.SaveChangesAsync();
        var svc = Service(db);
        var page = await svc.PendingAsync(new() { Search = " CLIENT ", Page = 2, PageSize = 1 });
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        var item = Assert.Single(page.Items);
        Assert.Equal(2, item.TravelRequestId);
        Assert.Null(item.ApproverName);
        Assert.Equal(3, (await svc.PendingAsync(new())).Items.Count); // Unscoped queue.
        Assert.Empty((await svc.PendingAsync(new() { Page = int.MaxValue })).Items);
        var normalized = await svc.PendingAsync(new() { Page = 0, PageSize = 999 });
        Assert.Equal(1, normalized.PageNumber);
        Assert.Equal(200, normalized.PageSize);
    }

    [Fact]
    public async Task Detail_ReturnsCurrentRequest_AllCyclesAndDocuments_WithoutMutationOrInternalIdentifiers()
    {
        using var db = Db();
        var request = Request(state: "Approved");
        request.CurrentCycleNo = 3;
        request.BusinessState = "Upcoming";
        request.ApprovedAt = DateTime.UtcNow;
        db.TravelRequests.Add(request);
        foreach (var no in new[] { 3, 1, 2 })
        {
            var cycle = Cycle(1, no, no == 3 ? "Approved" : "ChangesRequested");
            cycle.ChangeReason = no < 3 ? $"Revise {no}" : null;
            cycle.ChangesMade = no > 1 ? $"Updated {no}" : null;
            cycle.DecisionComment = $"Review {no}";
            cycle.DecisionBy = "director";
            cycle.DecisionAt = DateTime.UtcNow;
            db.TravelRequestCycles.Add(cycle);
        }
        db.TravelRequests.Add(Request(2));
        db.TravelRequestCycles.Add(Cycle(2, 1));
        db.Attachments.Add(new Attachment { RelatedModule = "Travel", RelatedEntity = "TravelRequest",
            RelatedEntityId = "1", OriginalFileName = "itinerary.pdf", ObjectKey = "Content/Travel/1/private.pdf",
            AccessUrl = "/Content/Travel/1/private.pdf", ContentType = "application/pdf", Size = 99,
            Metadata = "{\"DocumentCategory\":\"Itinerary\",\"CycleNo\":2}" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var beforeCycles = await db.TravelRequestCycles.CountAsync();
        var result = await Service(db).GetAsync(1);
        Assert.Equal(1, result.TravelRequestId);
        Assert.Equal(request.ReferenceNo, result.ReferenceNo);
        Assert.Equal("Upcoming", result.BusinessState);
        Assert.Equal("Approved", result.ApprovalState);
        Assert.Equal(request.ApprovedAt, result.ApprovedAt);
        Assert.Equal("Ada", result.Traveller.TravellerName);
        Assert.Equal("Client visit", result.Trip.Purpose);
        Assert.Equal("Air", result.Transportation.TransportType);
        Assert.Equal("Hotel A", result.Hotel.Hotel);
        Assert.True(result.LocalTransport.PickupRequired);
        Assert.Equal("Lunch", result.Hospitality.HospitalityRequirement);
        Assert.Equal("Arrive early", result.Itinerary.ItineraryNotes);
        Assert.Equal(350m, result.Budget.TotalEstimatedCost);
        Assert.Equal(new[] { 1, 2, 3 }, result.ApprovalHistory.Select(c => c.CycleNo));
        Assert.Equal(new[] { "ChangesRequested", "ChangesRequested", "Approved" }, result.ApprovalHistory.Select(c => c.DecisionState));
        Assert.Equal("Revise 1", result.ApprovalHistory[0].ChangeReason);
        Assert.Equal("Updated 2", result.ApprovalHistory[1].ChangesMade);
        Assert.Equal("Review 3", result.CurrentCycle!.DecisionComment);
        Assert.Equal(3, result.CurrentCycle.CycleNo);
        Assert.Equal("director", result.CurrentCycle.DecisionBy);
        var doc = Assert.Single(result.Documents);
        Assert.Equal("itinerary.pdf", doc.OriginalFileName);
        Assert.Equal("Itinerary", doc.DocumentCategory);
        Assert.Equal(2, doc.CycleNo);
        Assert.Equal($"/api/ea/travel/documents/{doc.Id}", doc.DownloadUrl);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var forbidden in new[] { "eaTaskId", "workflowInstanceId", "objectKey", "Content/", "travelRequestCycleId" })
            Assert.DoesNotContain(forbidden, json);
        using var parsed = JsonDocument.Parse(json);
        Assert.False(parsed.RootElement.GetProperty("currentCycle").TryGetProperty("id", out _));
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(101, (await db.TravelRequests.AsNoTracking().SingleAsync(r => r.Id == 1)).EaTaskId);
        Assert.Equal(beforeCycles, await db.TravelRequestCycles.CountAsync());
        Assert.Empty(db.Tasks);
        Assert.Empty(db.WorkflowInstances);
        Assert.Empty(db.ApprovalRequests);
        Assert.Empty(db.ApprovalCycles);
        Assert.Empty(db.AuditLogs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Detail_MissingOrDeleted_ReturnsNotFound(bool deleted)
    {
        using var db = Db();
        if (deleted) { var request = Request(); request.IsDeleted = true; db.TravelRequests.Add(request); await db.SaveChangesAsync(); }
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).GetAsync(1));
    }

    [Fact]
    public async Task Detail_PreSubmission_HasNoCurrentCycleAndNeedsNoDocuments()
    {
        using var db = Db();
        var request = Request(state: "NotSubmitted");
        request.CurrentCycleNo = 0;
        db.TravelRequests.Add(request);
        await db.SaveChangesAsync();
        var result = await Service(db).GetAsync(1);
        Assert.Null(result.CurrentCycle);
        Assert.Empty(result.ApprovalHistory);
        Assert.Empty(result.Documents);
        var controller = new TravelApprovalsController(Service(db));
        Assert.IsType<OkObjectResult>(await controller.Get(1, default));
        Assert.IsType<OkObjectResult>(await controller.Pending(new(), default));
    }
}
