using System;
using System.IO;
using System.Threading.Tasks;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Travel;

/// <summary>
/// Focused tests for Travel actor attribution under the CURRENT non-JWT EA architecture.
///
/// EA APIs do not use JWT authentication, so ICurrentUserService is never populated for
/// EA requests (UserId is always 0, UserName is always null) — a prior guard that treated
/// that as "unauthenticated, reject" made TravelRequestService.CreateDraftAsync and
/// TravelDocumentService.UploadAsync permanently unusable (every EA request always looked
/// anonymous). The fix: the frontend instead supplies its logged-in user's stable HRMS
/// User.Id (CreateTravelRequestDto.UserId / the upload form's userId field — the same id
/// GET /api/Users already returns), and IEaActorResolver resolves it against the existing
/// hrms_api.Data.AppDbContext Users table to the real FirstName+LastName, which becomes
/// TravelRequest.CreatedBy / Attachment.UploadedBy. The frontend can never submit a
/// display name directly — neither DTO exposes a name field, only the id. (The field was
/// originally named ActorUserId/actorUserId; renamed to the simpler UserId/userId — the
/// underlying resolution logic and IEaActorResolver are unchanged.)
///
/// IEaActorResolver's queries run against hrms_api.Data.AppDbContext, which maps at least
/// one entity through Npgsql's native JsonDocument/jsonb support — a real-provider feature
/// EF InMemory cannot emulate (model finalization throws "No suitable constructor was
/// found for entity type 'JsonDocument'"). So, like DelegationCreateCoreTransactionTests
/// and MeetingToDelegationTests before it, the HRMS side of these tests connects to the
/// same local dev Postgres instance every `dotnet ef` command in this project already
/// requires — read-only (no rows are inserted/deleted here), against real, already-existing
/// Users rows, so HR data is never touched or polluted. Travel/EaFmsDbContext-side state
/// still uses EF InMemory as before (CreateDraftAsync/UploadAsync do not use raw SQL there).
///
/// These tests verify the contract from all angles specified in the task:
///   1-2:  valid user id creates Travel → CreatedBy = backend-resolved FullName
///   3-4:  valid user id uploads        → UploadedBy = backend-resolved FullName
///   5-6:  neither DTO/contract exposes a settable display-name field (spoofing is structurally impossible)
///   7-8:  user A's id ≠ user B's resolved name (identity isolation)
///   9:    missing/zero user id is explicitly rejected, not silently persisted as "0"
///   10:   unknown user id is rejected, not silently accepted
///   11-12: existing Travel/document behavior for other flows is unaffected (verified by
///          the full TravelRequestServiceTests/TravelDocumentServiceTests suites, unchanged)
/// </summary>
public class TravelActorIdentityTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    // Real, pre-existing HRMS user rows (verified live via GET /api/Users before writing
    // these tests) — never inserted or deleted by this test file.
    private const int UserAId = 1;
    private const string UserAFullName = "Anurag Gupta";
    private const int UserBId = 2;
    private const string UserBFullName = "Anurag Test";
    private const int UnknownUserId = 999999;

    private const string HrmsConnectionString = "Host=localhost;Port=5432;Database=DB_Studio5Jarvis;Username=postgres;Password=123456";

    private static EaFmsDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static hrms_api.Data.AppDbContext MakeHrmsDb() =>
        new(new DbContextOptionsBuilder<hrms_api.Data.AppDbContext>()
            .UseNpgsql(HrmsConnectionString)
            .Options);

    // ──────────────────────────────────────────────────────────────
    // TravelRequestService factory
    // ──────────────────────────────────────────────────────────────

    private static TravelRequestService MakeTravelService(EaFmsDbContext db, hrms_api.Data.AppDbContext hrmsDb)
    {
        // ICurrentUserService is still injected (unrelated constructor dependency, and
        // still used by other, untouched methods on this service such as Update's
        // ModifiedBy) but is deliberately never populated here — proving the resolution
        // path no longer depends on it at all for Create.
        var user = Mock.Of<ICurrentUserService>(u => u.UserName == null && u.UserId == 0);
        var audit = Mock.Of<IAuditService>();

        var numbers = new Mock<ITravelNumberRepository>();
        var seq = 0;
        numbers.Setup(r => r.GenerateNextReferenceNoAsync(It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(() => $"TRV-TEST-{System.Threading.Interlocked.Increment(ref seq):D6}");

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
                    CreatedBy = "seeder",
                    CreatedDate = DateTime.UtcNow
                };
                db.Tasks.Add(task);
                db.SaveChanges();
                return new EaTaskResponseDto
                {
                    EaTaskId = task.Id,
                    ModuleId = 1,
                    ModuleName = task.ModuleName,
                    BusinessRecordId = task.BusinessRecordId,
                    Task = task.Task,
                    ExecutionStatus = task.ExecutionStatus,
                    IsActive = true,
                    CreatedBy = task.CreatedBy,
                    CreatedDate = task.CreatedDate
                };
            });

        var actorResolver = new EaActorResolver(hrmsDb);
        return new TravelRequestService(db, audit, user, numbers.Object, eaTasks.Object, actorResolver);
    }

    private static async Task<BusinessModule> SeedTravelModuleAsync(EaFmsDbContext db)
    {
        var module = new BusinessModule
        {
            Name = TravelRequestService.TravelBusinessModuleName,
            IsActive = true,
            IsDeleted = false,
            CreatedBy = "seeder",
            CreatedDate = DateTime.UtcNow
        };
        db.BusinessModules.Add(module);
        await db.SaveChangesAsync();
        return module;
    }

    private static CreateTravelRequestDto MinimalCreateDto(int? userId) => new()
    {
        UserId = userId,
        Travellers = new List<TravelTravellerDto> { new() { TravellerName = "Alice" } },
        Purpose = "Test trip",
        ApprovalRequired = false
    };

    // ──────────────────────────────────────────────────────────────
    // TravelDocumentService factory
    // ──────────────────────────────────────────────────────────────

    private IWebHostEnvironment MakeEnv()
    {
        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(temp);
        _tempDirs.Add(temp);
        return Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == temp);
    }

    private static TravelDocumentService MakeDocService(
        EaFmsDbContext db, IWebHostEnvironment env, hrms_api.Data.AppDbContext hrmsDb)
    {
        var user = Mock.Of<ICurrentUserService>(u => u.UserName == null && u.UserId == 0);
        var actorResolver = new EaActorResolver(hrmsDb);
        return new TravelDocumentService(db, user, Mock.Of<IAuditService>(), env, actorResolver);
    }

    private static TravelRequest SeedTravel(EaFmsDbContext db, long id = 1)
    {
        var entity = new TravelRequest
        {
            Id = id, ReferenceNo = $"TRV-SEED-{id:D6}", EaTaskId = 900 + id,
            BusinessState = "Draft", ApprovalState = "NotRequired", CurrentCycleNo = 0,
            CreatedBy = "seeder", CreatedDate = DateTime.UtcNow
        };
        db.TravelRequests.Add(entity);
        db.Tasks.Add(new EaTask
        {
            Id = 900 + id, BusinessModuleId = 1, ModuleName = "Travel & Hospitality",
            BusinessRecordId = id.ToString(), Task = $"TRV-SEED-{id:D6}",
            ExecutionStatus = "NotStarted", IsActive = true,
            CreatedBy = "seeder", CreatedDate = DateTime.UtcNow
        });
        db.SaveChanges();
        return entity;
    }

    private static IFormFile SmallPdf() =>
        new FormFile(
            new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }),
            0, 5, "file", "test.pdf")
        { Headers = new HeaderDictionary(), ContentType = "application/pdf" };

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
            try { Directory.Delete(dir, true); } catch { /* best-effort */ }
    }

    // ──────────────────────────────────────────────────────────────
    // 1. Valid existing user id creates Travel → CreatedBy = resolved FullName
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDraft_ValidUserId_CreatedByEqualsResolvedFullName()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        await SeedTravelModuleAsync(db);
        var svc = MakeTravelService(db, hrmsDb);

        var result = await svc.CreateDraftAsync(MinimalCreateDto(userId: UserAId), default);

        var persisted = await db.TravelRequests.FindAsync(result.TravelRequestId);
        Assert.Equal(UserAFullName, persisted!.CreatedBy);
    }

    // ──────────────────────────────────────────────────────────────
    // 2. Travel detail exposes the resolved creator display name
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_AfterCreate_DetailReturnsResolvedCreatorName()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        await SeedTravelModuleAsync(db);
        var svc = MakeTravelService(db, hrmsDb);

        var created = await svc.CreateDraftAsync(MinimalCreateDto(userId: UserAId), default);
        var detail = await svc.GetByIdAsync(created.TravelRequestId, default);

        Assert.Equal(UserAFullName, detail.CreatedBy);
    }

    // ──────────────────────────────────────────────────────────────
    // 3. Valid existing user id uploads document → UploadedBy = resolved FullName
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_ValidUserId_UploadedByEqualsResolvedFullName()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        SeedTravel(db, id: 1);
        var env = MakeEnv();
        var svc = MakeDocService(db, env, hrmsDb);

        var result = await svc.UploadAsync(1, SmallPdf(), null, userId: UserAId, default);

        Assert.Equal(UserAFullName, result.UploadedBy);
        var persisted = await db.Set<Attachment>().FindAsync(result.Id);
        Assert.Equal(UserAFullName, persisted!.UploadedBy);
        Assert.Equal(UserAFullName, persisted.CreatedBy);
    }

    // ──────────────────────────────────────────────────────────────
    // 4. Document response exposes the resolved uploader display name
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_ValidUserId_ResponseUploaderNameMatchesResolvedUser()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        SeedTravel(db, id: 2);
        var env = MakeEnv();
        var svc = MakeDocService(db, env, hrmsDb);

        var result = await svc.UploadAsync(2, SmallPdf(), "Itinerary", userId: UserBId, default);

        Assert.Equal(UserBFullName, result.UploadedBy);
    }

    // ──────────────────────────────────────────────────────────────
    // 5. Frontend cannot supply an arbitrary CreatedBy name
    //    (CreateTravelRequestDto exposes only UserId, never a name field)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDraft_DtoExposesOnlyUserId_NeverAWritableDisplayName()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        await SeedTravelModuleAsync(db);
        var svc = MakeTravelService(db, hrmsDb);

        var result = await svc.CreateDraftAsync(MinimalCreateDto(userId: UserAId), default);
        var persisted = await db.TravelRequests.FindAsync(result.TravelRequestId);

        Assert.Equal(UserAFullName, persisted!.CreatedBy);
        var dtoType = typeof(CreateTravelRequestDto);
        Assert.Null(dtoType.GetProperty("CreatedBy"));
        Assert.Null(dtoType.GetProperty("CreatedByName"));
        // The only actor-related property is the stable id, never a name.
        Assert.NotNull(dtoType.GetProperty("UserId"));
        Assert.Equal(typeof(int?), dtoType.GetProperty("UserId")!.PropertyType);
    }

    // ──────────────────────────────────────────────────────────────
    // 6. Frontend cannot supply an arbitrary UploadedBy name
    //    (UploadAsync's contract takes only userId, never a name)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_ServiceContractExposesOnlyUserId_NeverAWritableDisplayName()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        SeedTravel(db, id: 3);
        var env = MakeEnv();
        var svc = MakeDocService(db, env, hrmsDb);

        var result = await svc.UploadAsync(3, SmallPdf(), null, userId: UserAId, default);

        Assert.Equal(UserAFullName, result.UploadedBy);
        var uploadMethod = typeof(ITravelDocumentService).GetMethod(nameof(ITravelDocumentService.UploadAsync))!;
        var paramNames = uploadMethod.GetParameters().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("uploadedBy", paramNames);
        Assert.Contains("userId", paramNames);
    }

    // ──────────────────────────────────────────────────────────────
    // 7. User A's id creates Travel → CreatedBy is NOT User B's name
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDraft_UserA_ResponseDoesNotClaimUserB()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        await SeedTravelModuleAsync(db);
        var svcA = MakeTravelService(db, hrmsDb);

        var result = await svcA.CreateDraftAsync(MinimalCreateDto(userId: UserAId), default);

        var detail = await svcA.GetByIdAsync(result.TravelRequestId, default);
        Assert.Equal(UserAFullName, detail.CreatedBy);
        Assert.NotEqual(UserBFullName, detail.CreatedBy);
    }

    // ──────────────────────────────────────────────────────────────
    // 8. User A's id uploads → UploadedBy is NOT User B's name
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_UserA_ResponseDoesNotClaimUserB()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        SeedTravel(db, id: 4);
        var env = MakeEnv();
        var svcA = MakeDocService(db, env, hrmsDb);

        var result = await svcA.UploadAsync(4, SmallPdf(), null, userId: UserAId, default);

        Assert.Equal(UserAFullName, result.UploadedBy);
        Assert.NotEqual(UserBFullName, result.UploadedBy);
    }

    // ──────────────────────────────────────────────────────────────
    // 9a. Missing user id (null) is rejected — not silently persisted as "0"
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDraft_MissingUserId_ThrowsBusinessRuleException_NoPartialCommit()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        await SeedTravelModuleAsync(db);
        var svc = MakeTravelService(db, hrmsDb);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.CreateDraftAsync(MinimalCreateDto(userId: null), default));

        Assert.Contains("userId", ex.Message);
        Assert.Equal(0, await db.TravelRequests.CountAsync());
    }

    // ──────────────────────────────────────────────────────────────
    // 9b. User id 0 is rejected
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDraft_UserIdZero_ThrowsBusinessRuleException()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        await SeedTravelModuleAsync(db);
        var svc = MakeTravelService(db, hrmsDb);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.CreateDraftAsync(MinimalCreateDto(userId: 0), default));
        Assert.Equal(0, await db.TravelRequests.CountAsync());
    }

    // ──────────────────────────────────────────────────────────────
    // 9c. Missing user id for upload is rejected
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_MissingUserId_ThrowsBusinessRuleException_NoPartialCommit()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        SeedTravel(db, id: 5);
        var env = MakeEnv();
        var svc = MakeDocService(db, env, hrmsDb);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.UploadAsync(5, SmallPdf(), null, userId: null, default));

        Assert.Contains("userId", ex.Message);
        Assert.Equal(0, await db.Set<Attachment>().CountAsync());
    }

    // ──────────────────────────────────────────────────────────────
    // 10a. Unknown user id is rejected (does not exist in the Users source)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDraft_UnknownUserId_ThrowsBusinessRuleException_NoPartialCommit()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        await SeedTravelModuleAsync(db);
        var svc = MakeTravelService(db, hrmsDb);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.CreateDraftAsync(MinimalCreateDto(userId: UnknownUserId), default));

        Assert.Contains(UnknownUserId.ToString(), ex.Message);
        Assert.Contains("does not exist", ex.Message);
        Assert.Equal(0, await db.TravelRequests.CountAsync());
    }

    // ──────────────────────────────────────────────────────────────
    // 10b. Unknown user id for upload is rejected
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_UnknownUserId_ThrowsBusinessRuleException_NoPartialCommit_NoFileLeftOnDisk()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        SeedTravel(db, id: 6);
        var env = MakeEnv();
        var svc = MakeDocService(db, env, hrmsDb);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.UploadAsync(6, SmallPdf(), null, userId: UnknownUserId, default));

        Assert.Contains(UnknownUserId.ToString(), ex.Message);
        Assert.Equal(0, await db.Set<Attachment>().CountAsync());
        // The user is resolved before any file write, so nothing was written to disk either.
        var travelDir = Path.Combine(env.ContentRootPath, "Content", "Travel", "6");
        Assert.False(Directory.Exists(travelDir));
    }

    // ──────────────────────────────────────────────────────────────
    // 11. No fake person is ever stored for an invalid user id
    // ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(UnknownUserId)]
    public async Task CreateDraft_InvalidUserId_NeverStoresAFakePerson(int? userId)
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        await SeedTravelModuleAsync(db);
        var svc = MakeTravelService(db, hrmsDb);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.CreateDraftAsync(MinimalCreateDto(userId), default));

        var all = await db.TravelRequests.ToListAsync();
        Assert.DoesNotContain(all, t => t.CreatedBy is "0" or "Unknown" or "EA" or "Admin" or "System");
    }
}
