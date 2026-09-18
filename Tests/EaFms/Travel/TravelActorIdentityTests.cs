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
/// Focused tests for the Travel actor-identity fix.
///
/// Root cause: TravelRequestService.CreateDraftAsync and TravelDocumentService.UploadAsync
/// use _currentUser.UserName ?? _currentUser.UserId.ToString() to resolve the actor.
/// When no Bearer token is sent, UserName == null and UserId == 0, producing actor = "0"
/// which is then persisted to TravelRequest.CreatedBy and Attachment.UploadedBy.
///
/// Fix: both services now guard against the anonymous (UserId==0 && UserName==null) case
/// and throw BusinessRuleException rather than silently persisting "0".
///
/// These tests verify the contract from all 10 angles specified in the task:
///   1-2:  authenticated creation → correct creator identity and display name
///   3-4:  authenticated upload  → correct uploader identity and display name
///   5-6:  payload cannot spoof creator or uploader
///   7-8:  user A ≠ user B (identity isolation)
///   9:    anonymous behavior is explicitly rejected (not silently mapped to a real person)
///   10:   existing Travel tests remain unaffected (verified by the full suite run)
/// </summary>
public class TravelActorIdentityTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    // ──────────────────────────────────────────────────────────────
    // InMemory DB factory
    // ──────────────────────────────────────────────────────────────

    private static EaFmsDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    // ──────────────────────────────────────────────────────────────
    // TravelRequestService factory (mirrors TravelRequestServiceTests pattern)
    // ──────────────────────────────────────────────────────────────

    private static TravelRequestService MakeTravelService(
        EaFmsDbContext db, string? userName, long userId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.UserName).Returns(userName);
        user.SetupGet(u => u.UserId).Returns(userId);

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

        return new TravelRequestService(db, audit, user.Object, numbers.Object, eaTasks.Object);
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

    private static CreateTravelRequestDto MinimalCreateDto() => new()
    {
        TravellerName = "Alice",
        Purpose = "Test trip",
        ApprovalRequired = false
    };

    // ──────────────────────────────────────────────────────────────
    // TravelDocumentService factory (mirrors TravelDocumentServiceTests pattern)
    // ──────────────────────────────────────────────────────────────

    private IWebHostEnvironment MakeEnv()
    {
        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(temp);
        _tempDirs.Add(temp);
        return Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == temp);
    }

    private static TravelDocumentService MakeDocService(
        EaFmsDbContext db, IWebHostEnvironment env, string? userName, long userId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.UserName).Returns(userName);
        user.SetupGet(u => u.UserId).Returns(userId);
        return new TravelDocumentService(db, user.Object, Mock.Of<IAuditService>(), env);
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
    // 1. Authenticated user creates Travel → CreatedBy = user's name
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDraft_AuthenticatedUser_CreatedByEqualsUserName()
    {
        await using var db = MakeDb();
        await SeedTravelModuleAsync(db);
        var svc = MakeTravelService(db, userName: "Shivani Singh", userId: 7);

        var result = await svc.CreateDraftAsync(MinimalCreateDto(), default);

        var persisted = await db.TravelRequests.FindAsync(result.TravelRequestId);
        Assert.Equal("Shivani Singh", persisted!.CreatedBy);
    }

    // ──────────────────────────────────────────────────────────────
    // 2. Travel detail exposes the correct creator display name
    //    (CreatedBy string = the full name when authenticated)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_AfterAuthenticatedCreate_DetailReturnsCreatorName()
    {
        await using var db = MakeDb();
        await SeedTravelModuleAsync(db);
        var svc = MakeTravelService(db, userName: "Shivani Singh", userId: 7);

        var created = await svc.CreateDraftAsync(MinimalCreateDto(), default);
        var detail = await svc.GetByIdAsync(created.TravelRequestId, default);

        Assert.Equal("Shivani Singh", detail.CreatedBy);
    }

    // ──────────────────────────────────────────────────────────────
    // 3. Authenticated user uploads document → UploadedBy = user's name
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_AuthenticatedUser_UploadedByEqualsUserName()
    {
        await using var db = MakeDb();
        SeedTravel(db, id: 1);
        var env = MakeEnv();
        var svc = MakeDocService(db, env, userName: "Shivani Singh", userId: 7);

        var result = await svc.UploadAsync(1, SmallPdf(), null, default);

        Assert.Equal("Shivani Singh", result.UploadedBy);
        var persisted = await db.Set<Attachment>().FindAsync(result.Id);
        Assert.Equal("Shivani Singh", persisted!.UploadedBy);
        Assert.Equal("Shivani Singh", persisted.CreatedBy);
    }

    // ──────────────────────────────────────────────────────────────
    // 4. Document response exposes correct uploader display name
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_AuthenticatedUser_ResponseUploaderNameMatchesToken()
    {
        await using var db = MakeDb();
        SeedTravel(db, id: 2);
        var env = MakeEnv();
        var svc = MakeDocService(db, env, userName: "Ahmed Al-Rashid", userId: 5);

        var result = await svc.UploadAsync(2, SmallPdf(), "Itinerary", default);

        Assert.Equal("Ahmed Al-Rashid", result.UploadedBy);
    }

    // ──────────────────────────────────────────────────────────────
    // 5. Client cannot spoof CreatedBy through request payload
    //    (CreateTravelRequestDto has no CreatedBy field; actor is server-only)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDraft_PayloadHasNoCreatedByField_ServerActorAlwaysApplied()
    {
        await using var db = MakeDb();
        await SeedTravelModuleAsync(db);
        // Authenticate as "Real EA"; no way to inject a different name through the DTO.
        var svc = MakeTravelService(db, userName: "Real EA", userId: 10);

        var dto = MinimalCreateDto();
        // The DTO type has no CreatedBy property — confirmed by schema inspection.
        // This test documents the invariant: whatever the DTO contains, CreatedBy
        // must equal the authenticated actor, not a client-supplied value.
        var result = await svc.CreateDraftAsync(dto, default);
        var persisted = await db.TravelRequests.FindAsync(result.TravelRequestId);

        Assert.Equal("Real EA", persisted!.CreatedBy);
        // Confirm the DTO type does not expose a settable CreatedBy that could be abused.
        var dtoType = typeof(CreateTravelRequestDto);
        Assert.Null(dtoType.GetProperty("CreatedBy"));
    }

    // ──────────────────────────────────────────────────────────────
    // 6. Client cannot spoof UploadedBy through upload payload
    //    (UploadedBy is server-owned from ICurrentUserService, never from form fields)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_PayloadHasNoUploadedByField_ServerActorAlwaysApplied()
    {
        await using var db = MakeDb();
        SeedTravel(db, id: 3);
        var env = MakeEnv();
        var svc = MakeDocService(db, env, userName: "Real EA", userId: 10);

        // The controller accepts only (travelRequestId, IFormFile, documentCategory) —
        // no UploadedBy in the form fields. UploadedBy comes exclusively from the
        // server-side ICurrentUserService.
        var result = await svc.UploadAsync(3, SmallPdf(), null, default);

        Assert.Equal("Real EA", result.UploadedBy);
    }

    // ──────────────────────────────────────────────────────────────
    // 7. User A creates Travel → CreatedBy is NOT User B's name
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDraft_UserA_ResponseDoesNotClaimUserB()
    {
        await using var db = MakeDb();
        await SeedTravelModuleAsync(db);
        var svcA = MakeTravelService(db, userName: "Alice Smith", userId: 1);

        var result = await svcA.CreateDraftAsync(MinimalCreateDto(), default);

        var detail = await svcA.GetByIdAsync(result.TravelRequestId, default);
        Assert.Equal("Alice Smith", detail.CreatedBy);
        Assert.NotEqual("Bob Jones", detail.CreatedBy);
    }

    // ──────────────────────────────────────────────────────────────
    // 8. User A uploads document → UploadedBy is NOT User B's name
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_UserA_ResponseDoesNotClaimUserB()
    {
        await using var db = MakeDb();
        SeedTravel(db, id: 4);
        var env = MakeEnv();
        var svcA = MakeDocService(db, env, userName: "Alice Smith", userId: 1);

        var result = await svcA.UploadAsync(4, SmallPdf(), null, default);

        Assert.Equal("Alice Smith", result.UploadedBy);
        Assert.NotEqual("Bob Jones", result.UploadedBy);
    }

    // ──────────────────────────────────────────────────────────────
    // 9a. Anonymous Travel create (UserId=0, UserName=null) is rejected —
    //     not silently mapped to "0" or any fabricated name
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDraft_AnonymousRequest_ThrowsBusinessRuleException_NotSilent()
    {
        await using var db = MakeDb();
        await SeedTravelModuleAsync(db);
        // UserId=0 and UserName=null simulate an unauthenticated HTTP request.
        var svc = MakeTravelService(db, userName: null, userId: 0);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.CreateDraftAsync(MinimalCreateDto(), default));

        Assert.Contains("Authenticated user identity", ex.Message);

        // No TravelRequest was committed.
        Assert.Equal(0, await db.TravelRequests.CountAsync());
    }

    // ──────────────────────────────────────────────────────────────
    // 9b. Anonymous document upload (UserId=0, UserName=null) is rejected
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_AnonymousRequest_ThrowsBusinessRuleException_NotSilent()
    {
        await using var db = MakeDb();
        SeedTravel(db, id: 5);
        var env = MakeEnv();
        var svc = MakeDocService(db, env, userName: null, userId: 0);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.UploadAsync(5, SmallPdf(), null, default));

        Assert.Contains("Authenticated user identity", ex.Message);

        // No Attachment was committed.
        Assert.Equal(0, await db.Set<Attachment>().CountAsync());
    }

    // ──────────────────────────────────────────────────────────────
    // 9c. Non-zero UserId with no UserName falls back to ID string (not "0")
    //     — preserves behavior for any future system/service account with an ID
    //     but no display name.
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDraft_AuthenticatedByIdOnlyNoUserName_UsesIdAsActor()
    {
        await using var db = MakeDb();
        await SeedTravelModuleAsync(db);
        // UserId != 0 but no UserName — treated as authenticated (system account).
        var svc = MakeTravelService(db, userName: null, userId: 99);

        var result = await svc.CreateDraftAsync(MinimalCreateDto(), default);

        var persisted = await db.TravelRequests.FindAsync(result.TravelRequestId);
        // Falls back to UserId.ToString() — "99", not "0", not a fabricated name.
        Assert.Equal("99", persisted!.CreatedBy);
    }

    // ──────────────────────────────────────────────────────────────
    // 9d. Non-zero UserId with no UserName for upload falls back to ID
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_AuthenticatedByIdOnlyNoUserName_UsesIdAsActor()
    {
        await using var db = MakeDb();
        SeedTravel(db, id: 6);
        var env = MakeEnv();
        var svc = MakeDocService(db, env, userName: null, userId: 99);

        var result = await svc.UploadAsync(6, SmallPdf(), null, default);

        Assert.Equal("99", result.UploadedBy);
    }
}
