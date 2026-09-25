using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Jarvis5.Validators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Travel;

/// <summary>
/// Focused tests for structured Travel travellers: one travellers[] object per frontend
/// row (TravellerName + EmployeePersonId + Department + ContactInformation together),
/// stored in ea_travel_travellers, returned unchanged, searchable by traveller name.
/// All tests use EF InMemory.
/// </summary>
public class TravelMultiTravellerTests
{
    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private static EaFmsDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static TravelRequestService MakeService(EaFmsDbContext db, string actor = "ea-user")
    {
        var user = Mock.Of<ICurrentUserService>(u => u.UserName == actor && u.UserId == 42L);
        var audit = Mock.Of<IAuditService>();
        var numbers = new Mock<ITravelNumberRepository>();
        var seq = 0;
        numbers.Setup(r => r.GenerateNextReferenceNoAsync(It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(() => $"TRV-MT-{System.Threading.Interlocked.Increment(ref seq):D6}");

        var eaTasks = new Mock<IEaTaskService>();
        eaTasks.Setup(s => s.CreateWithoutTatAsync(
                It.IsAny<CreateEaTaskDto>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync((CreateEaTaskDto dto, System.Threading.CancellationToken _) =>
            {
                var task = new EaTask
                {
                    BusinessModuleId = 1,
                    ModuleName = TravelRequestService.TravelBusinessModuleName,
                    BusinessRecordId = dto.BusinessRecordId,
                    Task = dto.Task,
                    ExecutionStatus = "NotStarted",
                    IsActive = true,
                    CreatedBy = actor,
                    CreatedDate = DateTime.UtcNow
                };
                db.Tasks.Add(task);
                db.SaveChanges();
                return new EaTaskResponseDto
                {
                    EaTaskId = task.Id, ModuleId = 1,
                    ModuleName = task.ModuleName,
                    BusinessRecordId = task.BusinessRecordId,
                    Task = task.Task, ExecutionStatus = task.ExecutionStatus,
                    IsActive = true, CreatedBy = task.CreatedBy, CreatedDate = task.CreatedDate
                };
            });

        // EA APIs run without JWT; CreateDraftAsync resolves the actor via
        // IEaActorResolver rather than ICurrentUserService. These tests are about
        // traveller-name handling, not actor identity (that's TravelActorIdentityTests'
        // job), so any employee identity resolves to a fixed valid name here.
        var actorResolver = Mock.Of<IEaActorResolver>(r =>
            r.ResolveDisplayNameAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()) == Task.FromResult(actor));

        return new TravelRequestService(db, audit, user, numbers.Object, eaTasks.Object, actorResolver);
    }

    private static async Task<BusinessModule> SeedModuleAsync(EaFmsDbContext db)
    {
        var m = new BusinessModule
        {
            Name = TravelRequestService.TravelBusinessModuleName,
            IsActive = true, IsDeleted = false,
            CreatedBy = "seeder", CreatedDate = DateTime.UtcNow
        };
        db.BusinessModules.Add(m);
        await db.SaveChangesAsync();
        return m;
    }

    private static TravelRequest SeedTravel(EaFmsDbContext db, long id,
        params TravelTraveller[] travellers)
    {
        var e = new TravelRequest
        {
            Id = id, ReferenceNo = $"TRV-MT-SEED-{id:D6}", EaTaskId = 900 + id,
            BusinessState = "Draft", ApprovalState = "NotRequired", CurrentCycleNo = 0,
            Travellers = travellers.ToList(),
            CreatedBy = "seeder", CreatedDate = DateTime.UtcNow
        };
        db.TravelRequests.Add(e);
        db.Tasks.Add(new EaTask
        {
            Id = 900 + id, BusinessModuleId = 1, ModuleName = "Travel & Hospitality",
            BusinessRecordId = id.ToString(), Task = $"TRV-MT-SEED-{id:D6}",
            ExecutionStatus = "NotStarted", IsActive = true,
            CreatedBy = "seeder", CreatedDate = DateTime.UtcNow
        });
        db.SaveChanges();
        return e;
    }

    private static TravelTraveller Row(string? name, string? emp = null, string? dept = null, string? contact = null, int order = 0) =>
        new() { TravellerName = name, EmployeePersonId = emp, Department = dept, ContactInformation = contact, SortOrder = order };

    private static TravelTravellerDto Dto(string? name, string? emp = null, string? dept = null, string? contact = null) =>
        new() { TravellerName = name, EmployeePersonId = emp, Department = dept, ContactInformation = contact };

    private static async Task<TravelRequestDetailDto> CreateAndGetAsync(
        EaFmsDbContext db, TravelRequestService svc, List<TravelTravellerDto>? travellers, int? numberOfTravellers = null)
    {
        if (!await db.BusinessModules.AnyAsync()) await SeedModuleAsync(db);
        var created = await svc.CreateDraftAsync(new CreateTravelRequestDto
        {
            EmployeeId = "S5I-1001", EmployeeName = "EA User", Travellers = travellers, NumberOfTravellers = numberOfTravellers, ApprovalRequired = false
        }, default);
        return await svc.GetByIdAsync(created.TravelRequestId, default);
    }

    [Fact]
    public async Task Create_SingleTraveller_RoundTripsAllFourValues()
    {
        await using var db = MakeDb();
        var detail = await CreateAndGetAsync(db, MakeService(db),
            new() { Dto("Sakshi", "EMP001", "Finance", "9876543210") });

        var t = Assert.Single(detail.Travellers);
        Assert.Equal("Sakshi", t.TravellerName);
        Assert.Equal("EMP001", t.EmployeePersonId);
        Assert.Equal("Finance", t.Department);
        Assert.Equal("9876543210", t.ContactInformation);
    }

    [Fact]
    public async Task Create_TwoTravellers_ValuesStayGroupedPerTraveller()
    {
        await using var db = MakeDb();
        var detail = await CreateAndGetAsync(db, MakeService(db), new()
        {
            Dto("Sakshi", "EMP001", "Finance", "1111111111"),
            Dto("Shivani", "EMP002", "Operations", "2222222222")
        });

        Assert.Equal(2, detail.Travellers.Count);
        Assert.Equal(("Sakshi", "EMP001", "Finance", "1111111111"),
            (detail.Travellers[0].TravellerName, detail.Travellers[0].EmployeePersonId,
             detail.Travellers[0].Department, detail.Travellers[0].ContactInformation));
        Assert.Equal(("Shivani", "EMP002", "Operations", "2222222222"),
            (detail.Travellers[1].TravellerName, detail.Travellers[1].EmployeePersonId,
             detail.Travellers[1].Department, detail.Travellers[1].ContactInformation));
    }

    [Fact]
    public async Task Create_FiveTravellers_AllPersistedInSubmittedOrder()
    {
        await using var db = MakeDb();
        var rows = Enumerable.Range(1, 5).Select(i => Dto($"T{i}", $"E{i}", $"D{i}", $"C{i}")).ToList();
        var detail = await CreateAndGetAsync(db, MakeService(db), rows);

        Assert.Equal(new[] { "T1", "T2", "T3", "T4", "T5" }, detail.Travellers.Select(t => t.TravellerName));
        Assert.Equal(new[] { "E1", "E2", "E3", "E4", "E5" }, detail.Travellers.Select(t => t.EmployeePersonId));
    }

    [Fact]
    public async Task Create_NullAndPartialTravellerProperties_AreAcceptedAndStoredAsNull()
    {
        await using var db = MakeDb();
        var detail = await CreateAndGetAsync(db, MakeService(db), new()
        {
            Dto("Only Name"),
            Dto(null, null, "Only Dept"),
            Dto("  Padded  ", "  ", null, " 555 ")
        });

        Assert.Equal(3, detail.Travellers.Count);
        Assert.Null(detail.Travellers[0].EmployeePersonId);
        Assert.Null(detail.Travellers[1].TravellerName);
        Assert.Equal("Only Dept", detail.Travellers[1].Department);
        Assert.Equal("Padded", detail.Travellers[2].TravellerName);
        Assert.Null(detail.Travellers[2].EmployeePersonId);
        Assert.Equal("555", detail.Travellers[2].ContactInformation);
    }

    [Fact]
    public async Task Create_NullEmptyOrBlankRows_YieldNoTravellers()
    {
        await using var db = MakeDb();
        var svc = MakeService(db);
        Assert.Empty((await CreateAndGetAsync(db, svc, null)).Travellers);
        Assert.Empty((await CreateAndGetAsync(db, svc, new())).Travellers);
        Assert.Empty((await CreateAndGetAsync(db, svc, new() { Dto(" ", "", null, "  ") })).Travellers);
    }

    [Fact]
    public async Task Create_NumberOfTravellersIndependentOfTravellersCount()
    {
        await using var db = MakeDb();
        var detail = await CreateAndGetAsync(db, MakeService(db),
            new() { Dto("Lead"), Dto("Companion") }, numberOfTravellers: 5);

        Assert.Equal(2, detail.Travellers.Count);
        Assert.Equal(5, detail.Trip.NumberOfTravellers);
    }

    [Fact]
    public async Task Create_CreatedByStillResolvedFromActor()
    {
        await using var db = MakeDb();
        await SeedModuleAsync(db);
        var svc = MakeService(db, actor: "Shivani Singh");

        var created = await svc.CreateDraftAsync(new CreateTravelRequestDto
        {
            EmployeeId = "S5I-1007", EmployeeName = "Shivani Singh", Travellers = new() { Dto("Traveller X", "E1") }, ApprovalRequired = false
        }, default);

        Assert.Equal("Shivani Singh", (await db.TravelRequests.FindAsync(created.TravelRequestId))!.CreatedBy);
    }

    [Fact]
    public async Task UpdateDraft_ReplacesTravellerRows_KeepingGroupingAndOrder()
    {
        await using var db = MakeDb();
        var svc = MakeService(db);
        var detail = await CreateAndGetAsync(db, svc, new()
        {
            Dto("Sakshi", "EMP001", "Finance", "1"),
            Dto("Shivani", "EMP002", "Operations", "2")
        });

        var updated = await svc.UpdateDraftAsync(detail.Id, new UpdateTravelDraftDto
        {
            Travellers = new()
            {
                Dto("Shivani", "EMP002", "Ops-New", "22"),
                Dto("Meera", "EMP003", "HR", "3")
            },
            ApprovalRequired = false
        });

        Assert.Equal(new[] { "Shivani", "Meera" }, updated.Travellers.Select(t => t.TravellerName));
        Assert.Equal("Ops-New", updated.Travellers[0].Department);
        Assert.Equal("EMP003", updated.Travellers[1].EmployeePersonId);

        var reread = await svc.GetByIdAsync(detail.Id, default);
        Assert.Equal(2, reread.Travellers.Count);
        Assert.Equal(2, await db.TravelTravellers.CountAsync(t => t.TravelRequestId == detail.Id));
        Assert.DoesNotContain(reread.Travellers, t => t.TravellerName == "Sakshi");
    }

    [Fact]
    public async Task UpdateDraft_NullTravellers_ClearsAllRows()
    {
        await using var db = MakeDb();
        var svc = MakeService(db);
        var detail = await CreateAndGetAsync(db, svc, new() { Dto("A"), Dto("B") });

        var updated = await svc.UpdateDraftAsync(detail.Id,
            new UpdateTravelDraftDto { Travellers = null, ApprovalRequired = false });

        Assert.Empty(updated.Travellers);
        Assert.Equal(0, await db.TravelTravellers.CountAsync(t => t.TravelRequestId == detail.Id));
    }

    [Fact]
    public async Task List_ItemsCarryStructuredTravellers()
    {
        await using var db = MakeDb();
        SeedTravel(db, 5, Row("Delta", "E1", "Fin", "c1", 0), Row("Epsilon", "E2", "Ops", "c2", 1));

        var result = await MakeService(db).ListAsync(new TravelRequestListQueryDto(), default);

        var item = Assert.Single(result.Items);
        Assert.Equal(new[] { "Delta", "Epsilon" }, item.Travellers.Select(t => t.TravellerName));
        Assert.Equal("Ops", item.Travellers[1].Department);
    }

    [Fact]
    public async Task List_SearchMatchesAnyTravellerName()
    {
        await using var db = MakeDb();
        SeedTravel(db, 6, Row("Alice Smith", order: 0), Row("Bob Jones", order: 1));
        SeedTravel(db, 7, Row("Carol White"));
        var svc = MakeService(db);

        var result = await svc.ListAsync(new TravelRequestListQueryDto { Search = "bob" }, default);

        Assert.Equal(6, Assert.Single(result.Items).Id);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task List_TravellerNameFilter_MatchesAnyTraveller()
    {
        await using var db = MakeDb();
        SeedTravel(db, 8, Row("Alice Smith", order: 0), Row("Bob Jones", order: 1));
        SeedTravel(db, 9, Row("Carol White"));

        var result = await MakeService(db).ListAsync(new TravelRequestListQueryDto { TravellerName = "Jones" }, default);

        Assert.Equal(8, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task List_DepartmentFilter_MatchesAnyTravellersDepartment()
    {
        await using var db = MakeDb();
        SeedTravel(db, 10, Row("A", dept: "Finance", order: 0), Row("B", dept: "Operations", order: 1));
        SeedTravel(db, 11, Row("C", dept: "HR"));

        var result = await MakeService(db).ListAsync(new TravelRequestListQueryDto { Department = "operations" }, default);

        Assert.Equal(10, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task List_Paging_IsAppliedAfterTravellerFilter()
    {
        await using var db = MakeDb();
        for (var i = 1; i <= 5; i++) SeedTravel(db, 20 + i, Row($"Match {i}"));
        SeedTravel(db, 30, Row("Other"));

        var result = await MakeService(db).ListAsync(
            new TravelRequestListQueryDto { Search = "match", Page = 2, PageSize = 2 }, default);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task GetById_ApprovalFieldsUnaffectedByTravellers()
    {
        await using var db = MakeDb();
        SeedTravel(db, 12, Row("Person A"));

        var detail = await MakeService(db).GetByIdAsync(12, default);

        Assert.Equal("NotRequired", detail.Approval.State);
        Assert.False(detail.Approval.Required);
    }

    [Fact]
    public void Normalize_TrimsBlanksToNullDropsEmptyRowsAndNumbersOrder()
    {
        var rows = TravelRequestService.NormalizeTravellers(new()
        {
            Dto("  A  ", " E ", "", null),
            null!,
            Dto(" ", "", null, "  "),
            Dto("B")
        });

        Assert.Equal(2, rows.Count);
        Assert.Equal("A", rows[0].TravellerName);
        Assert.Equal("E", rows[0].EmployeePersonId);
        Assert.Null(rows[0].Department);
        Assert.Equal(new[] { 0, 1 }, rows.Select(r => r.SortOrder));
        Assert.Empty(TravelRequestService.NormalizeTravellers(null));
    }

    [Theory]
    [InlineData(201, 0, 0, 0, false)]
    [InlineData(200, 100, 200, 500, true)]
    [InlineData(0, 101, 0, 0, false)]
    [InlineData(0, 0, 201, 0, false)]
    [InlineData(0, 0, 0, 501, false)]
    public async Task Validators_EnforcePerTravellerLengthLimitsOnly(int nameLen, int empLen, int deptLen, int contactLen, bool valid)
    {
        var traveller = Dto(new string('n', nameLen), new string('e', empLen), new string('d', deptLen), new string('c', contactLen));
        var create = await new CreateTravelRequestDtoValidator().ValidateAsync(
            new CreateTravelRequestDto { Travellers = new() { Dto("ok"), traveller }, ApprovalRequired = false });
        var update = await new UpdateTravelDraftDtoValidator().ValidateAsync(
            new UpdateTravelDraftDto { Travellers = new() { traveller }, ApprovalRequired = false });

        Assert.Equal(valid, create.IsValid);
        Assert.Equal(valid, update.IsValid);
    }

    [Fact]
    public async Task Validators_AllTravellerPropertiesNullOrMissingList_AreValid()
    {
        var v = new CreateTravelRequestDtoValidator();
        Assert.True((await v.ValidateAsync(new CreateTravelRequestDto { Travellers = new() { Dto(null) }, ApprovalRequired = false })).IsValid);
        Assert.True((await v.ValidateAsync(new CreateTravelRequestDto { Travellers = null, ApprovalRequired = false })).IsValid);
    }
}
