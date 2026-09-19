using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
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
            // CreateDraftAsync opens a transaction (matching Meeting/Approval); InMemory
            // does not honor transactions and only warns about it.
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static (Mock<IAuditService> audit, Mock<ICurrentUserService> user) MakeMocks()
    {
        var audit = new Mock<IAuditService>();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.UserId).Returns(42L);
        user.SetupGet(u => u.UserName).Returns("testuser");
        return (audit, user);
    }

    /// <summary>
    /// EA APIs run without JWT, so CreateDraftAsync no longer resolves the actor from
    /// ICurrentUserService — it resolves CreateTravelRequestDto.UserId against the
    /// existing HRMS Users source via IEaActorResolver. Tests that exercise
    /// CreateDraftAsync but aren't specifically about actor-identity resolution (that's
    /// TravelActorIdentityTests' job) use this fixed, always-valid resolver so they keep
    /// testing what they already test.
    /// </summary>
    private static IEaActorResolver MakeActorResolver(string name = "Test EA User") =>
        Mock.Of<IEaActorResolver>(r =>
            r.ResolveDisplayNameAsync(It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult(name));

    /// <summary>
    /// Builds a service with unconfigured number/EaTask dependencies for tests that never
    /// call CreateDraftAsync (Moq returns default completed tasks for un-setup members).
    /// </summary>
    private static TravelRequestService MakeService(
        EaFmsDbContext db, Mock<IAuditService> audit, Mock<ICurrentUserService> user) =>
        new(db, audit.Object, user.Object, new Mock<ITravelNumberRepository>().Object, new Mock<IEaTaskService>().Object,
            new Mock<IEaActorResolver>().Object);

    /// <summary>
    /// Mocks matching the Approval test convention: reference numbers are generated
    /// in-memory, and the EaTask mock inserts a real row into the shared InMemory `db`
    /// (via EaTaskService.CreateWithoutTatAsync) so it gets a real identity Id, exactly
    /// as the real EaTaskService would inside the same transaction.
    /// </summary>
    private static (Mock<ITravelNumberRepository> numbers, Mock<IEaTaskService> eaTasks) MakeCreateMocks(
        EaFmsDbContext db, long moduleId)
    {
        var numbers = new Mock<ITravelNumberRepository>();
        var seq = 0;
        numbers.Setup(r => r.GenerateNextReferenceNoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"TRV-2026-{System.Threading.Interlocked.Increment(ref seq):D6}");

        var eaTasks = new Mock<IEaTaskService>();
        eaTasks.Setup(s => s.CreateWithoutTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateEaTaskDto dto, CancellationToken _) =>
            {
                var task = new EaTask
                {
                    BusinessModuleId = moduleId,
                    ModuleName = TravelRequestService.TravelBusinessModuleName,
                    BusinessRecordId = dto.BusinessRecordId,
                    Task = dto.Task,
                    Description = dto.Description,
                    AllottedTatMinutes = null,
                    ExecutionStatus = "NotStarted",
                    IsActive = true,
                    CreatedBy = "tester",
                    CreatedDate = DateTime.UtcNow
                };
                db.Tasks.Add(task);
                db.SaveChanges();
                return new EaTaskResponseDto
                {
                    EaTaskId = task.Id,
                    ModuleId = moduleId,
                    ModuleName = TravelRequestService.TravelBusinessModuleName,
                    BusinessRecordId = task.BusinessRecordId,
                    Task = task.Task,
                    Description = task.Description,
                    AllottedTatMinutes = null,
                    ExecutionStatus = task.ExecutionStatus,
                    IsActive = true,
                    CreatedBy = task.CreatedBy,
                    CreatedDate = task.CreatedDate
                };
            });

        return (numbers, eaTasks);
    }

    private static async Task<BusinessModule> AddTravelModuleAsync(EaFmsDbContext db)
    {
        var module = new BusinessModule
        {
            Name = TravelRequestService.TravelBusinessModuleName,
            IsActive = true,
            IsDeleted = false,
            CreatedBy = "tester",
            CreatedDate = DateTime.UtcNow
        };
        db.BusinessModules.Add(module);
        await db.SaveChangesAsync();
        return module;
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
    [InlineData(true, "NotSubmitted")]
    public void CreateDraft_ApprovalStateDependsOnApprovalRequired(bool approvalRequired, string expectedApprovalState)
    {
        // Draft semantics: Pending is reserved for future Submit, not Draft create/update.
        var approvalState = TravelRequestService.ResolveDraftApprovalState(approvalRequired);
        Assert.Equal(expectedApprovalState, approvalState);
        Assert.NotEqual("Pending", approvalState);
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

        var service = MakeService(db, audit, user);

        var dto = new UpdateTravelDraftDto
        {
            Travellers = new List<TravelTravellerDto> { new() { TravellerName = "John Doe" } },
            Purpose = "Client visit",
            ApprovalRequired = false
        };

        var result = await service.UpdateDraftAsync(entity.Id, dto);

        Assert.Equal("John Doe", Assert.Single(result.Travellers).TravellerName);
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

        var service = MakeService(db, audit, user);

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

        var service = MakeService(db, audit, user);

        var dto = new UpdateTravelDraftDto { Travellers = new List<TravelTravellerDto> { new() { TravellerName = "Alice" } }, ApprovalRequired = false };
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
            ApprovalState = "NotSubmitted",
            ApprovalRequired = true,
            ApproverId = "mgr-1",
            CreatedBy = "testuser",
            CreatedDate = DateTime.UtcNow
        };
        db.TravelRequests.Add(entity);
        await db.SaveChangesAsync();

        var service = MakeService(db, audit, user);

        var result = await service.UpdateDraftAsync(entity.Id,
            new UpdateTravelDraftDto { ApprovalRequired = false });

        Assert.Equal("NotRequired", result.ApprovalState);
        Assert.NotEqual("Pending", result.ApprovalState);
    }

    [Fact]
    public async Task UpdateDraft_WhenApprovalRequiredChangedToTrue_SetsApprovalStateNotSubmitted()
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

        var service = MakeService(db, audit, user);

        var result = await service.UpdateDraftAsync(entity.Id,
            new UpdateTravelDraftDto { ApprovalRequired = true, ApproverId = "mgr-2" });

        Assert.Equal("NotSubmitted", result.ApprovalState);
        Assert.NotEqual("Pending", result.ApprovalState);
    }

    [Fact]
    public async Task CreateDraft_WhenModuleMissing_ThrowsConfigurationBusinessRule_BeforePolicyGate()
    {
        await using var db = MakeDb();
        var (audit, user) = MakeMocks();
        var service = MakeService(db, audit, user);

        var ex = await Assert.ThrowsAsync<Jarvis5.Common.BusinessRuleException>(
            () => service.CreateDraftAsync(new CreateTravelRequestDto { ApprovalRequired = true }));

        Assert.Contains("TRAVEL BUSINESS MODULE CONFIGURATION REQUIRED BEFORE RUNTIME TRAVEL CREATION", ex.Message);
        Assert.Empty(db.TravelRequests);
        audit.Verify(a => a.AddAudit(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateDraft_WhenModulePresent_CreatesTravelRequestAndNoTatEaTask_InOneTransaction()
    {
        await using var db = MakeDb();
        var (audit, user) = MakeMocks();
        var module = await AddTravelModuleAsync(db);
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);

        var service = new TravelRequestService(db, audit.Object, user.Object, numbers.Object, eaTasks.Object, MakeActorResolver());

        var result = await service.CreateDraftAsync(new CreateTravelRequestDto
        {
            UserId = 1,
            Travellers = new List<TravelTravellerDto> { new() { TravellerName = "Sam" } },
            Purpose = "Client visit",
            ApprovalRequired = false
        });

        // TravelRequest.Id / EaTask.Id are real, non-zero, and exactly one of each exists.
        Assert.NotEqual(0, result.TravelRequestId);
        Assert.NotEqual(0, result.EaTaskId);
        Assert.Equal("Draft", result.BusinessState);
        Assert.Equal("NotRequired", result.ApprovalState);
        Assert.Single(db.TravelRequests);
        Assert.Single(db.Tasks);

        var persisted = await db.TravelRequests.SingleAsync();
        Assert.Equal(result.TravelRequestId, persisted.Id);
        Assert.Equal(result.EaTaskId, persisted.EaTaskId);
        Assert.Equal(0, persisted.CurrentCycleNo);

        // EaTask.BusinessRecordId must end up as the real TravelRequest.Id, not ReferenceNo.
        var task = await db.Tasks.SingleAsync();
        Assert.Equal(persisted.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), task.BusinessRecordId);
        Assert.Null(task.AllottedTatMinutes);
        Assert.Equal(persisted.ReferenceNo, task.Task);
        Assert.Equal("Client visit", task.Description);

        // No cycle is created on Draft creation.
        Assert.Empty(db.TravelRequestCycles);

        // The no-TAT path was used, never the TAT-required path — this is fixed backend
        // module policy, not something the caller/DTO can influence.
        eaTasks.Verify(s => s.CreateWithoutTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()), Times.Once);
        eaTasks.Verify(s => s.CreateAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()), Times.Never);

        audit.Verify(a => a.AddAudit(
            "TRAVEL_CREATE_DRAFT", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CreateDraft_WhenApprovalRequiredTrue_SetsApprovalStateNotSubmitted_NotPending()
    {
        await using var db = MakeDb();
        var (audit, user) = MakeMocks();
        var module = await AddTravelModuleAsync(db);
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var service = new TravelRequestService(db, audit.Object, user.Object, numbers.Object, eaTasks.Object, MakeActorResolver());

        var result = await service.CreateDraftAsync(
            new CreateTravelRequestDto { UserId = 1, ApprovalRequired = true, ApproverId = "mgr-1" });

        Assert.Equal("NotSubmitted", result.ApprovalState);
        Assert.NotEqual("Pending", result.ApprovalState);
    }

    [Fact]
    public async Task CreateDraft_ReferenceNo_UsesTrvFormat_AndIsBackendGenerated()
    {
        await using var db = MakeDb();
        var (audit, user) = MakeMocks();
        var module = await AddTravelModuleAsync(db);
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var service = new TravelRequestService(db, audit.Object, user.Object, numbers.Object, eaTasks.Object, MakeActorResolver());

        var result = await service.CreateDraftAsync(new CreateTravelRequestDto { UserId = 1 });

        Assert.Matches(@"^TRV-\d{4}-\d{6}$", result.ReferenceNo);
    }

    [Fact]
    public void CreateTravelRequestDto_DoesNotExposeBackendOwnedFields()
    {
        // Frontend can never supply EaTaskId/ReferenceNo/state — the DTO simply has no
        // such properties, so this cannot regress silently.
        var props = typeof(CreateTravelRequestDto).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.DoesNotContain("Id", props);
        Assert.DoesNotContain("EaTaskId", props);
        Assert.DoesNotContain("ReferenceNo", props);
        Assert.DoesNotContain("BusinessState", props);
        Assert.DoesNotContain("ApprovalState", props);
        Assert.DoesNotContain("CurrentCycleNo", props);
    }

    [Fact]
    public void TravelBusinessModule_CanonicalName_IsTravelAndHospitality()
    {
        Assert.Equal("Travel & Hospitality", TravelRequestService.TravelBusinessModuleName);
    }

    // ----------------------------------------------------------------
    // GetById: not found
    // ----------------------------------------------------------------

    [Fact]
    public async Task GetById_WhenNotFound_ThrowsNotFoundException()
    {
        await using var db = MakeDb();
        var (audit, user) = MakeMocks();
        var service = MakeService(db, audit, user);

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

        var service = MakeService(db, audit, user);

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
                Travellers = new List<TravelTraveller> { new() { TravellerName = "Alice Smith" } }, BusinessState = "Draft",
                ApprovalState = "NotRequired", ApprovalRequired = false,
                CreatedBy = "testuser", CreatedDate = DateTime.UtcNow
            },
            new TravelRequest
            {
                ReferenceNo = "TRV-2026-000011", EaTaskId = 2,
                Travellers = new List<TravelTraveller> { new() { TravellerName = "Bob Jones" } }, BusinessState = "Draft",
                ApprovalState = "NotRequired", ApprovalRequired = false,
                CreatedBy = "testuser", CreatedDate = DateTime.UtcNow
            }
        );
        await db.SaveChangesAsync();

        var service = MakeService(db, audit, user);
        var result = await service.ListAsync(new TravelRequestListQueryDto { Search = "alice" });

        Assert.Single(result.Items);
        Assert.Equal("Alice Smith", Assert.Single(result.Items[0].Travellers).TravellerName);
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

        var service = MakeService(db, audit, user);
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

        var service = MakeService(db, audit, user);
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

        var service = MakeService(db, audit, user);

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

        var service = MakeService(db, audit, user);
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
