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
/// EA requests (UserId is always 0, UserName is always null). The frontend therefore sends
/// its logged-in user's employee code and name from the HRMS login session
/// (CreateTravelRequestDto.EmployeeId/EmployeeName, and the upload form's employeeId/
/// employeeName fields). IEaActorResolver validates them and the name becomes
/// TravelRequest.CreatedBy / Attachment.UploadedBy. The HRMS Users table is not consulted
/// (its rows do not cover every EA login), so these tests need no database for the actor.
///
///   1-4:  valid identity creates/uploads → CreatedBy/UploadedBy = employee name
///   5-6:  DTO and upload contract expose employeeId + employeeName
///   7-8:  user A's identity ≠ user B's name (identity isolation)
///   9:    missing employeeId / employeeName is explicitly rejected, nothing persisted
///   10:   over-long names are rejected; surrounding whitespace is trimmed
///   11:   no fake person is ever stored for an invalid identity
/// </summary>
public class TravelActorIdentityTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    private const string UserAId = "S5I-1001";
    private const string UserAName = "Anurag Gupta";
    private const string UserBId = "EMP-001";
    private const string UserBName = "Anurag Test";

    // The resolver still receives the HRMS context through DI (FindUserContactAsync), but
    // ResolveDisplayNameAsync never queries it, so no connection is ever opened here.
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
        // ICurrentUserService is deliberately never populated here — proving the
        // resolution path does not depend on it for Create.
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

    private static CreateTravelRequestDto MinimalCreateDto(string? employeeId, string? employeeName) => new()
    {
        EmployeeId = employeeId,
        EmployeeName = employeeName,
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

    // 1. Valid identity creates Travel → CreatedBy = employee name
    [Fact]
    public async Task CreateDraft_ValidIdentity_CreatedByEqualsEmployeeName()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        await SeedTravelModuleAsync(db);
        var svc = MakeTravelService(db, hrmsDb);

        var result = await svc.CreateDraftAsync(MinimalCreateDto(UserAId, UserAName), default);

        var persisted = await db.TravelRequests.FindAsync(result.TravelRequestId);
        Assert.Equal(UserAName, persisted!.CreatedBy);
    }

    // 2. Travel detail exposes the creator name
    [Fact]
    public async Task GetById_AfterCreate_DetailReturnsCreatorName()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        await SeedTravelModuleAsync(db);
        var svc = MakeTravelService(db, hrmsDb);

        var created = await svc.CreateDraftAsync(MinimalCreateDto(UserAId, UserAName), default);
        var detail = await svc.GetByIdAsync(created.TravelRequestId, default);

        Assert.Equal(UserAName, detail.CreatedBy);
    }

    // 3. Valid identity uploads document → UploadedBy = employee name
    [Fact]
    public async Task Upload_ValidIdentity_UploadedByEqualsEmployeeName()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        SeedTravel(db, id: 1);
        var svc = MakeDocService(db, MakeEnv(), hrmsDb);

        var result = await svc.UploadAsync(1, SmallPdf(), null, UserAId, UserAName, default);

        Assert.Equal(UserAName, result.UploadedBy);
        var persisted = await db.Set<Attachment>().FindAsync(result.Id);
        Assert.Equal(UserAName, persisted!.UploadedBy);
        Assert.Equal(UserAName, persisted.CreatedBy);
    }

    // 4. Document response exposes the uploader name
    [Fact]
    public async Task Upload_ValidIdentity_ResponseUploaderNameMatchesEmployee()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        SeedTravel(db, id: 2);
        var svc = MakeDocService(db, MakeEnv(), hrmsDb);

        var result = await svc.UploadAsync(2, SmallPdf(), "Itinerary", UserBId, UserBName, default);

        Assert.Equal(UserBName, result.UploadedBy);
    }

    // 5. CreateTravelRequestDto carries the employee identity, never a CreatedBy field
    [Fact]
    public void CreateDto_ExposesEmployeeIdentity_NotCreatedBy()
    {
        var dtoType = typeof(CreateTravelRequestDto);
        Assert.Null(dtoType.GetProperty("CreatedBy"));
        Assert.Null(dtoType.GetProperty("UserId"));
        Assert.Equal(typeof(string), dtoType.GetProperty("EmployeeId")!.PropertyType);
        Assert.Equal(typeof(string), dtoType.GetProperty("EmployeeName")!.PropertyType);
    }

    // 6. Upload contract carries the employee identity, never an uploadedBy parameter
    [Fact]
    public void UploadContract_ExposesEmployeeIdentity_NotUploadedBy()
    {
        var uploadMethod = typeof(ITravelDocumentService).GetMethod(nameof(ITravelDocumentService.UploadAsync))!;
        var paramNames = uploadMethod.GetParameters().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("uploadedBy", paramNames);
        Assert.DoesNotContain("userId", paramNames);
        Assert.Contains("employeeId", paramNames);
        Assert.Contains("employeeName", paramNames);
    }

    // 7. User A creates Travel → CreatedBy is NOT User B's name
    [Fact]
    public async Task CreateDraft_UserA_ResponseDoesNotClaimUserB()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        await SeedTravelModuleAsync(db);
        var svc = MakeTravelService(db, hrmsDb);

        var result = await svc.CreateDraftAsync(MinimalCreateDto(UserAId, UserAName), default);

        var detail = await svc.GetByIdAsync(result.TravelRequestId, default);
        Assert.Equal(UserAName, detail.CreatedBy);
        Assert.NotEqual(UserBName, detail.CreatedBy);
    }

    // 8. User A uploads → UploadedBy is NOT User B's name
    [Fact]
    public async Task Upload_UserA_ResponseDoesNotClaimUserB()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        SeedTravel(db, id: 4);
        var svc = MakeDocService(db, MakeEnv(), hrmsDb);

        var result = await svc.UploadAsync(4, SmallPdf(), null, UserAId, UserAName, default);

        Assert.Equal(UserAName, result.UploadedBy);
        Assert.NotEqual(UserBName, result.UploadedBy);
    }

    // 9a. Missing employeeId is rejected — nothing persisted
    [Fact]
    public async Task CreateDraft_MissingEmployeeId_ThrowsBusinessRuleException_NoPartialCommit()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        await SeedTravelModuleAsync(db);
        var svc = MakeTravelService(db, hrmsDb);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.CreateDraftAsync(MinimalCreateDto(null, UserAName), default));

        Assert.Contains("employeeId", ex.Message);
        Assert.Equal(0, await db.TravelRequests.CountAsync());
    }

    // 9b. Missing employeeName is rejected
    [Fact]
    public async Task CreateDraft_MissingEmployeeName_ThrowsBusinessRuleException()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        await SeedTravelModuleAsync(db);
        var svc = MakeTravelService(db, hrmsDb);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.CreateDraftAsync(MinimalCreateDto(UserAId, "   "), default));

        Assert.Contains("employeeName", ex.Message);
        Assert.Equal(0, await db.TravelRequests.CountAsync());
    }

    // 9c. Missing identity for upload is rejected, and nothing is written to disk
    [Fact]
    public async Task Upload_MissingIdentity_ThrowsBusinessRuleException_NoPartialCommit_NoFileLeftOnDisk()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        SeedTravel(db, id: 5);
        var env = MakeEnv();
        var svc = MakeDocService(db, env, hrmsDb);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.UploadAsync(5, SmallPdf(), null, null, null, default));

        Assert.Contains("employeeId", ex.Message);
        Assert.Equal(0, await db.Set<Attachment>().CountAsync());
        Assert.False(Directory.Exists(Path.Combine(env.ContentRootPath, "Content", "Travel", "5")));
    }

    // 10a. Surrounding whitespace in the name is trimmed
    [Fact]
    public async Task CreateDraft_NameIsTrimmed()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        await SeedTravelModuleAsync(db);
        var svc = MakeTravelService(db, hrmsDb);

        var result = await svc.CreateDraftAsync(MinimalCreateDto(UserAId, $"  {UserAName}  "), default);

        Assert.Equal(UserAName, (await db.TravelRequests.FindAsync(result.TravelRequestId))!.CreatedBy);
    }

    // 10b. Names longer than the CreatedBy column are rejected, not truncated
    [Fact]
    public async Task CreateDraft_NameTooLong_ThrowsBusinessRuleException()
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        await SeedTravelModuleAsync(db);
        var svc = MakeTravelService(db, hrmsDb);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.CreateDraftAsync(MinimalCreateDto(UserAId, new string('x', 101)), default));
        Assert.Equal(0, await db.TravelRequests.CountAsync());
    }

    // 11. No fake person is ever stored for an invalid identity
    [Theory]
    [InlineData(null, null)]
    [InlineData("", UserAName)]
    [InlineData(UserAId, "")]
    public async Task CreateDraft_InvalidIdentity_NeverStoresAFakePerson(string? employeeId, string? employeeName)
    {
        await using var db = MakeDb();
        await using var hrmsDb = MakeHrmsDb();
        await SeedTravelModuleAsync(db);
        var svc = MakeTravelService(db, hrmsDb);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.CreateDraftAsync(MinimalCreateDto(employeeId, employeeName), default));

        var all = await db.TravelRequests.ToListAsync();
        Assert.DoesNotContain(all, t => t.CreatedBy is "0" or "Unknown" or "EA" or "Admin" or "System");
    }
}
