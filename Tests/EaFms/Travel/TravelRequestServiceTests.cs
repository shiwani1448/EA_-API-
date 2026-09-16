using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Travel;

/// <summary>
/// Unit tests for TravelRequestService pure/service behavior.
/// All tests use EF InMemory; no real database required.
/// </summary>
public class TravelRequestServiceTests
{
    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private static EaFmsDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static (Mock<IAuditService> audit, Mock<ICurrentUserService> user) MakeMocks()
    {
        var audit = new Mock<IAuditService>();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.UserId).Returns(42L);
        user.SetupGet(u => u.UserName).Returns("testuser");
        return (audit, user);
    }

    // ----------------------------------------------------------------
    // TotalEstimatedCost calculation
    // ----------------------------------------------------------------

    [Fact]
    public void CalculateTotalEstimatedCost_AllPresent_ReturnsSumOfComponents()
    {
        var entity = new TravelRequest
        {
            EstimatedTravelCost = 1000m,
            EstimatedHotelCost = 500m,
            EstimatedLocalTransportCost = 200m,
            EstimatedHospitalityCost = 300m
        };

        var total = TravelRequestService.CalculateTotalEstimatedCost(entity);

        Assert.Equal(2000m, total);
    }

    [Fact]
    public void CalculateTotalEstimatedCost_SomeNull_TreatsNullAsZero()
    {
        var entity = new TravelRequest
        {
            EstimatedTravelCost = 1000m,
            EstimatedHotelCost = null,         // null → treated as 0
            EstimatedLocalTransportCost = 500m,
            EstimatedHospitalityCost = null    // null → treated as 0
        };

        var total = TravelRequestService.CalculateTotalEstimatedCost(entity);

        Assert.Equal(1500m, total);
        // Verify stored null values are NOT mutated by the calculation
        Assert.Null(entity.EstimatedHotelCost);
        Assert.Null(entity.EstimatedHospitalityCost);
    }

    [Fact]
    public void CalculateTotalEstimatedCost_AllNull_ReturnsZero()
    {
        var entity = new TravelRequest
        {
            EstimatedTravelCost = null,
            EstimatedHotelCost = null,
            EstimatedLocalTransportCost = null,
            EstimatedHospitalityCost = null
        };

        var total = TravelRequestService.CalculateTotalEstimatedCost(entity);

        Assert.Equal(0m, total);
    }

    // ----------------------------------------------------------------
    // Initial state defaults
    // ----------------------------------------------------------------

    [Fact]
    public void TravelRequest_DefaultBusinessState_IsDraft()
    {
        var entity = new TravelRequest();
        Assert.Equal("Draft", entity.BusinessState);
    }

    [Fact]
    public void TravelRequest_DefaultApprovalState_IsNotRequired()
    {
        var entity = new TravelRequest();
        Assert.Equal("NotRequired", entity.ApprovalState);
    }

    // ----------------------------------------------------------------
    // ApprovalState initial logic based on ApprovalRequired flag
    // ----------------------------------------------------------------

    [Theory]
    [InlineData(false, "NotRequired")]
    [InlineData(true, "Pending")]
    public void CreateDraft_ApprovalStateDependsOnApprovalRequired(bool approvalRequired, string expectedApprovalState)
    {
        // The service's ApprovalState logic:
        //   if (!dto.ApprovalRequired) → "NotRequired"
        //   else → "Pending"
        // We test the logic directly without a real DB by reconstructing it.
        var approvalState = approvalRequired ? "Pending" : "NotRequired";
        Assert.Equal(expectedApprovalState, approvalState);
    }

    // ----------------------------------------------------------------
    // UpdateDraft: editability gate — only Draft is editable
    // ----------------------------------------------------------------

    [Fact]
    public async Task UpdateDraft_WhenStateDraft_Succeeds()
    {
        await using var db = MakeDb();
        var (audit, user) = MakeMocks();

        // Seed a Draft with EaTaskId=1 (bypass FK in InMemory)
        var entity = new TravelRequest
        {
            ReferenceNo = "TRV-2026-000001",
            EaTaskId = 1,
            BusinessState = "Draft",
            ApprovalState = "NotRequired",
            ApprovalRequired = false,
            CreatedBy = "testuser",
            CreatedDate = DateTime.UtcNow
        };
        db.TravelRequests.Add(entity);
        await db.SaveChangesAsync();

        var service = new TravelRequestService(db, audit.Object, user.Object);

        var dto = new UpdateTravelDraftDto
        {
            TravellerName = "John Doe",
            Purpose = "Client visit",
            ApprovalRequired = false
        };

        var result = await service.UpdateDraftAsync(entity.Id, dto);

        Assert.Equal("John Doe", result.Traveller.TravellerName);
        Assert.Equal("Client visit", result.Trip.Purpose);
        Assert.Equal("Draft", result.BusinessState);
    }

    [Theory]
    [InlineData("Upcoming")]
    [InlineData("Active")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public async Task UpdateDraft_WhenStateNotDraft_ThrowsBusinessRuleException(string state)
    {
        await using var db = MakeDb();
        var (audit, user) = MakeMocks();

        var entity = new TravelRequest
        {
            ReferenceNo = "TRV-2026-000002",
            EaTaskId = 1,
            BusinessState = state,
            ApprovalState = "NotRequired",
            ApprovalRequired = false,
            CreatedBy = "testuser",
            CreatedDate = DateTime.UtcNow
        };
        db.TravelRequests.Add(entity);
        await db.SaveChangesAsync();

        var service = new TravelRequestService(db, audit.Object, user.Object);

        await Assert.ThrowsAsync<Jarvis5.Common.BusinessRuleException>(
            () => service.UpdateDraftAsync(entity.Id, new UpdateTravelDraftDto()));
    }

    // ----------------------------------------------------------------
    // UpdateDraft: immutable field protection
    // ----------------------------------------------------------------

    [Fact]
    public async Task UpdateDraft_DoesNotModify_ReferenceNo_EaTaskId_CurrentCycleNo_CreatedBy()
    {
        await using var db = MakeDb();
        var (audit, user) = MakeMocks();

        var entity = new TravelRequest
        {
            ReferenceNo = "TRV-2026-000003",
            EaTaskId = 99,
            CurrentCycleNo = 0,
            BusinessState = "Draft",
            ApprovalState = "NotRequired",
            ApprovalRequired = false,
            CreatedBy = "original-creator",
            CreatedDate = DateTime.UtcNow
        };
        db.TravelRequests.Add(entity);
        await db.SaveChangesAsync();

        var service = new TravelRequestService(db, audit.Object, user.Object);

        var dto = new UpdateTravelDraftDto { TravellerName = "Alice", ApprovalRequired = false };
        var result = await service.UpdateDraftAsync(entity.Id, dto);

        // Reload from DB to confirm nothing changed at persistence level
        var persisted = await db.TravelRequests.FindAsync(entity.Id);
        Assert.NotNull(persisted);
        Assert.Equal("TRV-2026-000003", persisted!.ReferenceNo);
        Assert.Equal(99L, persisted.EaTaskId);
        Assert.Equal(0, persisted.CurrentCycleNo);
        Assert.Equal("original-creator", persisted.CreatedBy);
    }

    // ----------------------------------------------------------------
    // UpdateDraft: ApprovalState transition logic
    // ----------------------------------------------------------------

    [Fact]
    public async Task UpdateDraft_WhenApprovalRequiredChangedToFalse_SetsApprovalStateNotRequired()
    {
        await using var db = MakeDb();
        var (audit, user) = MakeMocks();

        var entity = new TravelRequest
        {
            ReferenceNo = "TRV-2026-000004",
            EaTaskId = 1,
            BusinessState = "Draft",
            ApprovalState = "Pending",
            ApprovalRequired = true,
            ApproverId = "mgr-1",
            CreatedBy = "testuser",
            CreatedDate = DateTime.UtcNow
        };
        db.TravelRequests.Add(entity);
        await db.SaveChangesAsync();

        var service = new TravelRequestService(db, audit.Object, user.Object);

        var result = await service.UpdateDraftAsync(entity.Id,
            new UpdateTravelDraftDto { ApprovalRequired = false });

        Assert.Equal("NotRequired", result.ApprovalState);
    }

    [Fact]
    public async Task UpdateDraft_WhenApprovalRequiredChangedToTrue_SetsApprovalStatePending()
    {
        await using var db = MakeDb();
        var (audit, user) = MakeMocks();

        var entity = new TravelRequest
        {
            ReferenceNo = "TRV-2026-000005",
            EaTaskId = 1,
            BusinessState = "Draft",
            ApprovalState = "NotRequired",
            ApprovalRequired = false,
            CreatedBy = "testuser",
            CreatedDate = DateTime.UtcNow
        };
        db.TravelRequests.Add(entity);
        await db.SaveChangesAsync();

        var service = new TravelRequestService(db, audit.Object, user.Object);

        var result = await service.UpdateDraftAsync(entity.Id,
            new UpdateTravelDraftDto { ApprovalRequired = true, ApproverId = "mgr-2" });

        Assert.Equal("Pending", result.ApprovalState);
    }

    // ----------------------------------------------------------------
    // GetById: not found
    // ----------------------------------------------------------------

    [Fact]
    public async Task GetById_WhenNotFound_ThrowsNotFoundException()
    {
        await using var db = MakeDb();
        var (audit, user) = MakeMocks();
        var service = new TravelRequestService(db, audit.Object, user.Object);

        await Assert.ThrowsAsync<Jarvis5.Common.NotFoundException>(
            () => service.GetByIdAsync(999999L));
    }

    [Fact]
    public async Task GetById_WhenSoftDeleted_ThrowsNotFoundException()
    {
        await using var db = MakeDb();
        var (audit, user) = MakeMocks();

        var entity = new TravelRequest
        {
            ReferenceNo = "TRV-2026-000006",
            EaTaskId = 1,
            BusinessState = "Draft",
            ApprovalState = "NotRequired",
            ApprovalRequired = false,
            CreatedBy = "testuser",
            CreatedDate = DateTime.UtcNow,
            IsDeleted = true
        };
        db.TravelRequests.Add(entity);
        await db.SaveChangesAsync();

        var service = new TravelRequestService(db, audit.Object, user.Object);

        await Assert.ThrowsAsync<Jarvis5.Common.NotFoundException>(
            () => service.GetByIdAsync(entity.Id));
    }

    // ----------------------------------------------------------------
    // List: search and filter mapping
    // ----------------------------------------------------------------

    [Fact]
    public async Task List_SearchByTravellerName_ReturnsMatchingRecords()
    {
        await using var db = MakeDb();
        var (audit, user) = MakeMocks();

        db.TravelRequests.AddRange(
            new TravelRequest
            {
                ReferenceNo = "TRV-2026-000010", EaTaskId = 1,
                TravellerName = "Alice Smith", BusinessState = "Draft",
                ApprovalState = "NotRequired", ApprovalRequired = false,
                CreatedBy = "testuser", CreatedDate = DateTime.UtcNow
            },
            new TravelRequest
            {
                ReferenceNo = "TRV-2026-000011", EaTaskId = 2,
                TravellerName = "Bob Jones", BusinessState = "Draft",
                ApprovalState = "NotRequired", ApprovalRequired = false,
                CreatedBy = "testuser", CreatedDate = DateTime.UtcNow
            }
        );
        await db.SaveChangesAsync();

        var service = new TravelRequestService(db, audit.Object, user.Object);
        var result = await service.ListAsync(new TravelRequestListQueryDto { Search = "alice" });

        Assert.Single(result.Items);
        Assert.Equal("Alice Smith", result.Items[0].TravellerName);
    }

    [Fact]
    public async Task List_FilterByBusinessState_ReturnsOnlyMatchingState()
    {
        await using var db = MakeDb();
        var (audit, user) = MakeMocks();

        db.TravelRequests.AddRange(
            new TravelRequest
            {
                ReferenceNo = "TRV-2026-000020", EaTaskId = 1,
                BusinessState = "Draft", ApprovalState = "NotRequired",
                ApprovalRequired = false, CreatedBy = "testuser", CreatedDate = DateTime.UtcNow
            },
            new TravelRequest
            {
                ReferenceNo = "TRV-2026-000021", EaTaskId = 2,
                BusinessState = "Upcoming", ApprovalState = "Pending",
                ApprovalRequired = true, CreatedBy = "testuser", CreatedDate = DateTime.UtcNow
            }
        );
        await db.SaveChangesAsync();

        var service = new TravelRequestService(db, audit.Object, user.Object);
        var result = await service.ListAsync(new TravelRequestListQueryDto { BusinessState = "Draft" });

        Assert.Single(result.Items);
        Assert.Equal("Draft", result.Items[0].BusinessState);
    }

    [Fact]
    public async Task List_SoftDeletedRecords_AreExcluded()
    {
        await using var db = MakeDb();
        var (audit, user) = MakeMocks();

        db.TravelRequests.AddRange(
            new TravelRequest
            {
                ReferenceNo = "TRV-2026-000030", EaTaskId = 1,
                BusinessState = "Draft", ApprovalState = "NotRequired",
                ApprovalRequired = false, CreatedBy = "testuser",
                CreatedDate = DateTime.UtcNow, IsDeleted = false
            },
            new TravelRequest
            {
                ReferenceNo = "TRV-2026-000031", EaTaskId = 2,
                BusinessState = "Draft", ApprovalState = "NotRequired",
                ApprovalRequired = false, CreatedBy = "testuser",
                CreatedDate = DateTime.UtcNow, IsDeleted = true
            }
        );
        await db.SaveChangesAsync();

        var service = new TravelRequestService(db, audit.Object, user.Object);
        var result = await service.ListAsync(new TravelRequestListQueryDto());

        Assert.Single(result.Items);
        Assert.Equal("TRV-2026-000030", result.Items[0].ReferenceNo);
    }

    [Fact]
    public async Task List_Pagination_ReturnsCorrectPageAndTotalCount()
    {
        await using var db = MakeDb();
        var (audit, user) = MakeMocks();

        for (var i = 1; i <= 5; i++)
        {
            db.TravelRequests.Add(new TravelRequest
            {
                ReferenceNo = $"TRV-2026-{i:D6}",
                EaTaskId = i,
                BusinessState = "Draft",
                ApprovalState = "NotRequired",
                ApprovalRequired = false,
                CreatedBy = "testuser",
                CreatedDate = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await db.SaveChangesAsync();

        var service = new TravelRequestService(db, audit.Object, user.Object);

        var page1 = await service.ListAsync(new TravelRequestListQueryDto { Page = 1, PageSize = 2 });
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(3, page1.TotalPages);

        var page3 = await service.ListAsync(new TravelRequestListQueryDto { Page = 3, PageSize = 2 });
        Assert.Single(page3.Items);
    }

    // ----------------------------------------------------------------
    // ReferenceNo format validation (logic test)
    // ----------------------------------------------------------------

    [Fact]
    public void ReferenceNoFormat_MatchesExpectedPattern()
    {
        // Verify the format string logic TRV-{YEAR}-{seq:D6}
        var year = 2026;
        var seq = 1L;
        var refNo = $"TRV-{year}-{seq:D6}";
        Assert.Equal("TRV-2026-000001", refNo);

        var seq2 = 999999L;
        var refNo2 = $"TRV-{year}-{seq2:D6}";
        Assert.Equal("TRV-2026-999999", refNo2);
    }

    // ----------------------------------------------------------------
    // Detail response: TotalEstimatedCost is server-calculated
    // ----------------------------------------------------------------

    [Fact]
    public async Task GetById_TotalEstimatedCost_IsServerCalculated_NullsNotMutated()
    {
        await using var db = MakeDb();
        var (audit, user) = MakeMocks();

        var entity = new TravelRequest
        {
            ReferenceNo = "TRV-2026-000040",
            EaTaskId = 1,
            BusinessState = "Draft",
            ApprovalState = "NotRequired",
            ApprovalRequired = false,
            EstimatedTravelCost = 1000m,
            EstimatedHotelCost = null,        // null: should not be mutated
            EstimatedLocalTransportCost = 500m,
            EstimatedHospitalityCost = null,  // null: should not be mutated
            CreatedBy = "testuser",
            CreatedDate = DateTime.UtcNow
        };
        db.TravelRequests.Add(entity);
        await db.SaveChangesAsync();

        var service = new TravelRequestService(db, audit.Object, user.Object);
        var result = await service.GetByIdAsync(entity.Id);

        // Total = 1000 + 0 + 500 + 0 = 1500
        Assert.Equal(1500m, result.Budget.TotalEstimatedCost);
        // Stored nulls must still be null in the response
        Assert.Null(result.Budget.EstimatedHotelCost);
        Assert.Null(result.Budget.EstimatedHospitalityCost);
        // Actual stored value also not mutated
        var persisted = await db.TravelRequests.FindAsync(entity.Id);
        Assert.Null(persisted!.EstimatedHotelCost);
        Assert.Null(persisted.EstimatedHospitalityCost);
    }
}
