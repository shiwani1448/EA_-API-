using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Delegation;

/// <summary>
/// §0: actor identity comes from the HRMS token via the real CurrentUserService — the Delegation
/// Actual phase's StartedById/StartedByName are the token's employee id/name, never "0", never body fields.
/// </summary>
public class DelegationTokenActorTests
{
    private static ICurrentUserService TokenUser(params Claim[] claims) => new CurrentUserService(
        new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")) } });

    private static async Task<(EaFmsDbContext Db, DelegationService Svc)> NewAsync(ICurrentUserService user)
    {
        var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
        var module = new BusinessModule { Name = DelegationService.DelegationBusinessModuleName, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.BusinessModules.Add(module);
        foreach (var name in new[] { "Captured", "In Progress", "Completed" })
            db.Statuses.Add(new Status { Name = name, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var numbers = new Mock<IDelegationNumberRepository>();
        numbers.Setup(r => r.GenerateNextReferenceNoAsync(It.IsAny<CancellationToken>())).ReturnsAsync("DLG-T-000001");
        var tasks = new Mock<IEaTaskService>();
        tasks.Setup(s => s.CreateWithoutTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateEaTaskDto dto, CancellationToken _) =>
            {
                var t = new EaTask { BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = dto.BusinessRecordId,
                    Task = dto.Task ?? "t", ExecutionStatus = "NotStarted", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
                db.Tasks.Add(t); db.SaveChanges();
                return new EaTaskResponseDto { EaTaskId = t.Id, ModuleId = module.Id, ModuleName = t.ModuleName, BusinessRecordId = t.BusinessRecordId,
                    Task = t.Task, ExecutionStatus = t.ExecutionStatus, IsActive = true, CreatedBy = t.CreatedBy, CreatedDate = t.CreatedDate };
            });
        var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == Path.GetTempPath());
        var audit = new AuditService(db, user);
        var svc = new DelegationService(db, user, audit, numbers.Object, tasks.Object, env,
            new TaskReviewService(db, new TaskReviewRepository(db), user, audit), new TatRuleRepository(db));
        return (db, svc);
    }

    [Fact]
    public async Task Start_ActualPhaseActor_ComesFromTokenClaims()
    {
        var user = TokenUser(new Claim("employeeID", "S5I-1013"), new Claim("employeeName", "Siddhi Jadhav"));
        var (db, svc) = await NewAsync(user);
        var created = await svc.CreateAsync(new DelegationCreateRequestDto { Title = "Prepare deck", DoerId = "emp-1", EndDate = DateTime.UtcNow.AddDays(5) });

        await svc.StartAsync(created.DelegationId);

        var phase = await db.DelegationPhaseTats.AsNoTracking().SingleAsync(p => p.DelegationId == created.DelegationId && p.TaskType == DelegationTaskType.Actual);
        Assert.Equal(("S5I-1013", "Siddhi Jadhav"), (phase.StartedById, phase.StartedByName));
    }

    [Fact]
    public async Task Start_WithZeroOrMissingToken_NeverStoresZero()
    {
        var user = TokenUser(new Claim("sub", "0"));
        var (db, svc) = await NewAsync(user);
        var created = await svc.CreateAsync(new DelegationCreateRequestDto { Title = "Prepare deck", DoerId = "emp-1", EndDate = DateTime.UtcNow.AddDays(5) });

        await svc.StartAsync(created.DelegationId);

        var phase = await db.DelegationPhaseTats.AsNoTracking().SingleAsync(p => p.DelegationId == created.DelegationId);
        Assert.Null(phase.StartedById);
        Assert.Null(phase.StartedByName);
    }
}
