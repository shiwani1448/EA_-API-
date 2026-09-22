using System;
using System.Linq;
using System.Threading.Tasks;
using Jarvis5.Data.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Jarvis5.Validators;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Delegation;

/// <summary>
/// Step 5B-2: proves DelegationService.CreateCoreAsync's transaction-composition contract
/// (never begins/commits/rolls back its own transaction; fully participates in the
/// caller's) against REAL PostgreSQL transaction semantics. EF InMemory does not
/// implement transactions or rollback at all — every other test in this suite that needs
/// real relational behavior (raw SQL locks, sequences, actual rollback) already works
/// around that by mocking; this is the one place mocking cannot substitute for the real
/// database, so unlike the rest of the suite, these four tests connect to the same local
/// dev Postgres instance every `dotnet ef` command in this project already requires (the
/// connection string is the one already committed in appsettings.json — not a secret).
/// They depend on the "Delegation" and "Meeting" BusinessModule rows configured in Step
/// 5B-1 already existing in that database. Every row created is clearly titled
/// "(disposable Step 5B-2 test)" with a unique-per-run marker.
/// </summary>
public class DelegationCreateCoreTransactionTests
{
    private const string ConnectionString = "Host=localhost;Port=5432;Database=DB_Studio5Jarvis;Username=postgres;Password=123456";

    private static EaFmsDbContext MakeRealDb() =>
        new(new DbContextOptionsBuilder<EaFmsDbContext>().UseNpgsql(ConnectionString).Options);

    private static DelegationService MakeRealService(EaFmsDbContext db)
    {
        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "step5b2-test" && u.UserId == 1);
        var audit = new AuditService(db, user);
        var eaTasks = new EaTaskService(db, new EaTaskRepository(db), new TatRuleRepository(db),
            new CreateEaTaskDtoValidator(), user, audit);
        return new DelegationService(db, user, audit, new DelegationRepository(db), eaTasks,
            Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(),
            new TaskReviewService(db, new TaskReviewRepository(db), user, audit));
    }

    private static async Task<long> ResolveMeetingModuleIdAsync(EaFmsDbContext db) =>
        await db.BusinessModules.Where(m => m.Name == "Meeting" && m.IsActive && !m.IsDeleted).Select(m => m.Id).SingleAsync();

    private static DelegationCreateCommand MakeCommand(string marker, long sourceModuleId) => new()
    {
        Title = $"Step 5B-2 transaction test {marker} (disposable Step 5B-2 test)",
        DoerId = "step5b2-doer",
        DoerNameSnapshot = "Step 5B-2 Tester",
        SourceBusinessModuleId = sourceModuleId,
        SourceEntityId = marker
    };

    [Fact]
    public async Task CreateCoreAsync_InsideCallerOwnedTransaction_ThenCommit_PersistsDelegationEaTaskAndAudit()
    {
        await using var db = MakeRealDb();
        var meetingModuleId = await ResolveMeetingModuleIdAsync(db);
        var service = MakeRealService(db);
        var marker = $"commit-{Guid.NewGuid():N}";

        await using var tx = await db.Database.BeginTransactionAsync();
        var result = await service.CreateCoreAsync(MakeCommand(marker, meetingModuleId), default);
        await tx.CommitAsync();

        await using var verifyDb = MakeRealDb();
        var delegation = await verifyDb.Delegations.SingleAsync(d => d.Id == result.DelegationId);
        Assert.Equal("Pending", delegation.Status);
        Assert.Null(delegation.StartedAt);
        Assert.Null(delegation.CompletedAt);

        var task = await verifyDb.Tasks.SingleAsync(t => t.Id == result.EaTaskId);
        Assert.Equal("Delegation", task.ModuleName);
        Assert.Equal(delegation.Id.ToString(), task.BusinessRecordId);
        Assert.Equal("NotStarted", task.ExecutionStatus);
        Assert.Null(task.StartedAt);
        Assert.Null(task.CompletedAt);
        Assert.Null(task.TatRuleId);
        Assert.Null(task.AllottedTatMinutes);
        Assert.Null(task.TatUsedMinutes);
        Assert.Null(task.WorkflowInstanceId);

        Assert.True(await verifyDb.AuditLogs.AnyAsync(a =>
            a.ActionType == "DELEGATION_CREATE" && a.EntityId == delegation.Id.ToString()));
    }

    [Fact]
    public async Task CreateCoreAsync_InsideCallerOwnedTransaction_ThenRollback_PersistsNothing()
    {
        await using var db = MakeRealDb();
        var meetingModuleId = await ResolveMeetingModuleIdAsync(db);
        var service = MakeRealService(db);
        var marker = $"rollback-{Guid.NewGuid():N}";

        await using var tx = await db.Database.BeginTransactionAsync();
        var result = await service.CreateCoreAsync(MakeCommand(marker, meetingModuleId), default);
        // Prove it really was written (intermediate SaveChanges), visible inside the still-open transaction...
        Assert.True(await db.Delegations.AnyAsync(d => d.Id == result.DelegationId));
        // ...then the caller (not CreateCoreAsync itself) decides to roll back.
        await tx.RollbackAsync();

        await using var verifyDb = MakeRealDb();
        Assert.False(await verifyDb.Delegations.AnyAsync(d => d.Id == result.DelegationId));
        Assert.False(await verifyDb.Tasks.AnyAsync(t => t.Id == result.EaTaskId));
        Assert.False(await verifyDb.AuditLogs.AnyAsync(a =>
            a.ActionType == "DELEGATION_CREATE" && a.EntityId == result.DelegationId.ToString()));
    }

    [Fact]
    public async Task CreateCoreAsync_TwiceInsideOneOuterTransaction_ThenCommit_PersistsBothIndependently()
    {
        await using var db = MakeRealDb();
        var meetingModuleId = await ResolveMeetingModuleIdAsync(db);
        var service = MakeRealService(db);
        var markerA = $"multiA-{Guid.NewGuid():N}";
        var markerB = $"multiB-{Guid.NewGuid():N}";

        await using var tx = await db.Database.BeginTransactionAsync();
        var a = await service.CreateCoreAsync(MakeCommand(markerA, meetingModuleId), default);
        var b = await service.CreateCoreAsync(MakeCommand(markerB, meetingModuleId), default);
        await tx.CommitAsync();

        Assert.NotEqual(a.DelegationId, b.DelegationId);
        Assert.NotEqual(a.EaTaskId, b.EaTaskId);
        Assert.NotEqual(a.ReferenceNo, b.ReferenceNo);
        Assert.Equal(markerA, a.SourceEntityId);
        Assert.Equal(markerB, b.SourceEntityId);

        await using var verifyDb = MakeRealDb();
        Assert.True(await verifyDb.Delegations.AnyAsync(d => d.Id == a.DelegationId));
        Assert.True(await verifyDb.Delegations.AnyAsync(d => d.Id == b.DelegationId));
        Assert.True(await verifyDb.Tasks.AnyAsync(t => t.Id == a.EaTaskId));
        Assert.True(await verifyDb.Tasks.AnyAsync(t => t.Id == b.EaTaskId));
        Assert.Equal(2, await verifyDb.AuditLogs.CountAsync(x => x.ActionType == "DELEGATION_CREATE" &&
            (x.EntityId == a.DelegationId.ToString() || x.EntityId == b.DelegationId.ToString())));
    }

    [Fact]
    public async Task CreateCoreAsync_TwiceInsideOneOuterTransaction_ThenRollback_PersistsNeither()
    {
        await using var db = MakeRealDb();
        var meetingModuleId = await ResolveMeetingModuleIdAsync(db);
        var service = MakeRealService(db);
        var markerA = $"multiRbA-{Guid.NewGuid():N}";
        var markerB = $"multiRbB-{Guid.NewGuid():N}";

        await using var tx = await db.Database.BeginTransactionAsync();
        var a = await service.CreateCoreAsync(MakeCommand(markerA, meetingModuleId), default);
        var b = await service.CreateCoreAsync(MakeCommand(markerB, meetingModuleId), default);
        // Simulate a later failure in the caller's own logic (e.g. a future Meeting
        // completion step failing after some, but not all, Delegations were created) —
        // this is exactly the scenario Step 5B-3 must be safe against.
        await tx.RollbackAsync();

        await using var verifyDb = MakeRealDb();
        Assert.False(await verifyDb.Delegations.AnyAsync(d => d.Id == a.DelegationId));
        Assert.False(await verifyDb.Delegations.AnyAsync(d => d.Id == b.DelegationId));
        Assert.False(await verifyDb.Tasks.AnyAsync(t => t.Id == a.EaTaskId));
        Assert.False(await verifyDb.Tasks.AnyAsync(t => t.Id == b.EaTaskId));
        Assert.Equal(0, await verifyDb.AuditLogs.CountAsync(x => x.ActionType == "DELEGATION_CREATE" &&
            (x.EntityId == a.DelegationId.ToString() || x.EntityId == b.DelegationId.ToString())));
    }
}
