using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.TaskReview;

/// <summary>
/// Per-phase TAT (Actual / Review N / Rework N): each phase resolves its own TAT rule — by
/// (DelegationType, TaskType) — and gets its own frozen Allotted/Used/Paused/PauseCount once it
/// closes, while the current open phase computes the same values live. IEaTaskService is mocked
/// (its own real implementation's advisory-lock line is Postgres-only and untestable here — see
/// DelegationTypeOnlyTatTests for that), but ITatRuleRepository is real: DelegationService's own
/// Review/Rework phase resolution calls it directly, and TatRuleRepository's IsRelational() guard
/// (see its own doc comment) makes that path exercisable against EF InMemory.
/// </summary>
public class DelegationPhaseTatTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    public void Dispose() { try { Directory.Delete(_root, true); } catch { /* best-effort */ } }

    private sealed class Fx
    {
        public required EaFmsDbContext Db { get; init; }
        public required DelegationService Svc { get; init; }
        public required long ModuleId { get; init; }
    }

    private async Task<Fx> NewAsync(bool persistAudit = false)
    {
        var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
        var module = new BusinessModule { Name = DelegationService.DelegationBusinessModuleName, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.BusinessModules.Add(module);
        db.Statuses.Add(new Status { Name = "In Progress", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "phase-actor" && u.UserId == 99L);
        var audit = persistAudit ? new AuditService(db, user) : Mock.Of<IAuditService>();
        var numbers = new Mock<IDelegationNumberRepository>();
        var seq = 0;
        numbers.Setup(r => r.GenerateNextReferenceNoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => $"DLG-PH-{++seq:D6}");
        var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == _root);

        // Simulates the Actual phase's TAT rule already having resolved to 30 minutes at creation —
        // the real EaTaskService.CreateWithTypeOnlyTatAsync does this for real (see
        // DelegationTypeOnlyTatTests), but its advisory-lock line only works against Postgres.
        var tasks = new Mock<IEaTaskService>();
        tasks.Setup(s => s.CreateWithTypeOnlyTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateEaTaskDto dto, CancellationToken _) =>
            {
                var t = new EaTask
                {
                    BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = dto.BusinessRecordId,
                    Task = dto.Task ?? dto.BusinessRecordId, Type = dto.Type, AllottedTatMinutes = 30,
                    ExecutionStatus = EaTaskExecutionStatus.NotStarted, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow
                };
                db.Tasks.Add(t); db.SaveChanges();
                return new EaTaskResponseDto { EaTaskId = t.Id, ModuleId = module.Id, ModuleName = t.ModuleName, BusinessRecordId = t.BusinessRecordId,
                    Task = t.Task, ExecutionStatus = t.ExecutionStatus, AllottedTatMinutes = t.AllottedTatMinutes, IsActive = true, CreatedBy = t.CreatedBy, CreatedDate = t.CreatedDate };
            });

        var tatRules = new TatRuleRepository(db);
        var taskReview = new TaskReviewService(db, new TaskReviewRepository(db), user, audit);
        var svc = new DelegationService(db, user, audit, numbers.Object, tasks.Object, env, taskReview, tatRules);

        return new Fx { Db = db, Svc = svc, ModuleId = module.Id };
    }

    [Fact]
    public async Task PhaseExecution_FullCycle_RecordsActorsAndServerReviewerAndAudit()
    {
        var f = await NewAsync(persistAudit: true);
        var created = await f.Svc.CreateAsync(new DelegationCreateRequestDto { Title = "Execution", DelegationType = "Test" });
        var id = created.DelegationId;
        AssertPhase(created, "NotStarted", null, null);
        AssertPhase(await f.Svc.StartAsync(id), "InProgress", "Actual", 0);
        AssertPhase(await f.Svc.CompleteAsync(id, null), "NotStarted", "Review", 1);
        AssertPhase(await f.Svc.StartReviewAsync(id), "InProgress", "Review", 1);
        var reworked = await f.Svc.RequestReworkAsync(id, new RequestTaskReworkRequestDto
        { ReviewedById = "spoofed", ReviewedByName = "Spoofed", ReworkRemark = "Fix it" }, null);
        AssertPhase(reworked, "NotStarted", "Rework", 1);
        Assert.Equal("phase-actor", reworked.ReviewSummary.ReviewedById);
        Assert.Equal("phase-actor", reworked.ReviewSummary.ReviewedByName);
        var audit = await f.Db.AuditLogs.SingleAsync(a => a.ActionType == "DELEGATION_REWORK_REQUESTED");
        Assert.Equal(id.ToString(), audit.EntityId);
        using (var oldValues = System.Text.Json.JsonDocument.Parse(audit.OldValues!))
            Assert.Equal("PendingReview", oldValues.RootElement.GetProperty("reviewStatus").GetString());
        using (var newValues = System.Text.Json.JsonDocument.Parse(audit.NewValues!))
        {
            Assert.Equal(1, newValues.RootElement.GetProperty("reviewCycle").GetInt32());
            Assert.Equal("Fix it", newValues.RootElement.GetProperty("reworkRemark").GetString());
            Assert.False(newValues.RootElement.GetProperty("hasAttachment").GetBoolean());
        }
        AssertPhase(await f.Svc.StartReworkAsync(id), "InProgress", "Rework", 1);
        AssertPhase(await f.Svc.CompleteAsync(id, null), "NotStarted", "Review", 2);
        AssertPhase(await f.Svc.StartReviewAsync(id), "InProgress", "Review", 2);
        var task = await f.Db.Tasks.SingleAsync(t => t.Id == created.EaTaskId);
        Assert.Equal("InProgress", task.ExecutionStatus);
        var approved = await f.Svc.ApproveReviewAsync(id, new ApproveTaskReviewRequestDto
        { ReviewedById = "spoofed", ReviewedByName = "Spoofed" }, null);
        AssertPhase(approved, "Completed", null, null);
        Assert.Equal("phase-actor", approved.ReviewSummary.ReviewedById);
        Assert.Equal("phase-actor", approved.ReviewSummary.ReviewedByName);
        Assert.Equal("Completed", task.ExecutionStatus);
        Assert.All(approved.PhaseTat, p =>
        {
            Assert.NotNull(p.StartedAt);
            Assert.NotNull(p.EndedAt);
            Assert.Equal("99", p.StartedById);   // §0: id from token
            Assert.Equal("phase-actor", p.StartedByName);
            Assert.Equal("99", p.EndedById);
            Assert.Equal("phase-actor", p.EndedByName);
        });
        Assert.All(await f.Db.DelegationPhaseTats.AsNoTracking().ToListAsync(), p =>
        {
            Assert.Equal("99", p.StartedById);   // §0: id from token
            Assert.Equal("99", p.EndedById);
        });
    }

    private static void AssertPhase(DelegationResponseDto dto, string execution, string? phase, int? cycle)
    {
        Assert.Equal(execution, dto.ExecutionStatus);
        Assert.Equal(phase, dto.CurrentPhase);
        Assert.Equal(cycle, dto.CurrentPhaseCycleNumber);
        Assert.Equal(phase is null ? null : dto.PhaseTat.Single(p => p.EndedAt == null).StartedAt,
            dto.CurrentPhaseStartedAt);
        if (phase is not null) Assert.Equal(DelegationStatus.InProgress, dto.Status);
    }

    [Fact]
    public async Task PhaseViewsAndSummary_MatchSingleAndBatchResponses_AndPausedIsStarted()
    {
        var f = await NewAsync();
        async Task<long> Create() => (await f.Svc.CreateAsync(new DelegationCreateRequestDto
            { Title = "Views", DelegationType = "Test" })).DelegationId;
        var pending = await Create();
        var running = await Create();
        await f.Svc.StartAsync(running);
        var paused = await f.Svc.PauseAsync(running, null);
        Assert.True(paused.IsPaused);
        AssertPhase(paused, "InProgress", "Actual", 0);
        var review = await Create();
        await f.Svc.StartAsync(review);
        await f.Svc.CompleteAsync(review, null);
        var rework = await Create();
        await f.Svc.StartAsync(rework);
        await f.Svc.CompleteAsync(rework, null);
        await f.Svc.RequestReworkAsync(rework, new RequestTaskReworkRequestDto(), null);
        var done = await Create();
        await f.Svc.StartAsync(done);
        await f.Svc.CompleteAsync(done, null);
        await f.Svc.ApproveReviewAsync(done, new ApproveTaskReviewRequestDto(), null);
        var closed = await f.Svc.GetByIdAsync(done);
        var unstartedReview = closed.PhaseTat.Single(p => p.TaskType == "Review");
        Assert.Null(unstartedReview.StartedById);
        Assert.Null(unstartedReview.StartedByName);
        Assert.Equal("99", unstartedReview.EndedById);
        var notStarted = await f.Svc.ListAsync(new DelegationListQueryDto { View = "notstarted" });
        Assert.Equal(new[] { pending, review, rework }.OrderBy(i => i), notStarted.Items.Select(d => d.DelegationId).OrderBy(i => i));
        Assert.All(notStarted.Items, d => Assert.Equal("NotStarted", d.ExecutionStatus));
        foreach (var row in notStarted.Items)
        {
            var single = await f.Svc.GetByIdAsync(row.DelegationId);
            Assert.Equal(single.CurrentPhase, row.CurrentPhase);
            Assert.Equal(single.CurrentPhaseCycleNumber, row.CurrentPhaseCycleNumber);
            Assert.Equal(single.CurrentPhaseStartedAt, row.CurrentPhaseStartedAt);
            Assert.Equal(single.PhaseTat.Select(p => p.EndedById), row.PhaseTat.Select(p => p.EndedById));
        }
        var started = await f.Svc.ListAsync(new DelegationListQueryDto { View = "started" });
        Assert.Equal(running, Assert.Single(started.Items).DelegationId);
        Assert.True(started.Items.Single().IsPaused);
        var summary = await f.Svc.GetSummaryAsync();
        Assert.Equal(3, summary.NotStarted);
        Assert.Equal(1, summary.Pending);
        Assert.Equal(3, summary.InProgress);
        Assert.Equal(1, summary.Completed);
        Assert.Equal(3, (await f.Svc.ListAsync(new DelegationListQueryDto { View = "inprogress" })).TotalCount);
        await Assert.ThrowsAsync<BadRequestException>(() => f.Svc.ListAsync(new DelegationListQueryDto { View = "notstarted", Status = "Completed" }));
    }

    [Theory]
    [InlineData("IsNotStartedExpr")]
    [InlineData("IsExecutionInProgressExpr")]
    public void PhaseViewPredicates_TranslateToPostgresExists(string method)
    {
        using var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseNpgsql("Host=localhost;Database=model_validation;Username=unused;Password=unused").Options);
        var svc = new DelegationService(db, Mock.Of<ICurrentUserService>(), Mock.Of<IAuditService>(),
            Mock.Of<IDelegationNumberRepository>(), Mock.Of<IEaTaskService>(), Mock.Of<IWebHostEnvironment>(),
            Mock.Of<ITaskReviewService>(), new TatRuleRepository(db));
        var predicate = (System.Linq.Expressions.Expression<Func<Jarvis5.Entities.EaFms.Delegation, bool>>)
            typeof(DelegationService).GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(svc, null)!;
        var sql = db.Delegations.Where(predicate).ToQueryString();
        Assert.Contains("EXISTS", sql);
        Assert.Contains("ea_delegation_phase_tat", sql);
        Assert.Contains("\"StartedAt\" IS NULL", sql);
        Assert.Contains("\"EndedAt\" IS NULL", sql);
    }

    [Fact]
    public void ActorMigration_AddsOnlyFourNullableColumns_WithMatchingLengths()
    {
        using var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseNpgsql("Host=localhost;Database=model_validation;Username=unused;Password=unused").Options);
        var assembly = db.GetService<IMigrationsAssembly>();
        var migration = assembly.CreateMigration(assembly.Migrations["20260924051830_AddDelegationPhaseActors"], db.Database.ProviderName!);
        Assert.Equal(4, migration.UpOperations.Count);
        Assert.Equal(new[] { "EndedById", "EndedByName", "StartedById", "StartedByName" },
            migration.UpOperations.Cast<Microsoft.EntityFrameworkCore.Migrations.Operations.AddColumnOperation>()
                .Select(c => c.Name).OrderBy(n => n));
        foreach (var operation in migration.UpOperations)
        {
            var column = Assert.IsType<Microsoft.EntityFrameworkCore.Migrations.Operations.AddColumnOperation>(operation);
            Assert.Equal("ea_delegation_phase_tat", column.Table);
            Assert.True(column.IsNullable);
            var length = column.Name.EndsWith("Id") ? 100 : 200;
            Assert.Equal(length, column.MaxLength);
            Assert.Equal(length, db.Model.FindEntityType(typeof(DelegationPhaseTat))!.FindProperty(column.Name)!.GetMaxLength());
        }
        Assert.Equal(4, migration.DownOperations.Count);
    }

    private async Task AddRuleAsync(Fx f, string type, string taskType, int minutes)
    {
        f.Db.TatRules.Add(new TatRule
        {
            BusinessModuleId = f.ModuleId, ModuleName = "Delegation", Type = type, Subtype = null, TaskType = taskType,
            TatMinutes = minutes, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task FullCycle_EachPhaseGetsItsOwnAllottedUsedAndDifference()
    {
        var f = await NewAsync();
        const string type = "Phase Test";
        await AddRuleAsync(f, type, DelegationTaskType.Review, 15);
        await AddRuleAsync(f, type, DelegationTaskType.Rework, 20);

        var created = await f.Svc.CreateAsync(new DelegationCreateRequestDto { Title = "Phase test", DoerId = "emp-1", DelegationType = type, EndDate = DateTime.UtcNow.AddDays(5) });
        var started = await f.Svc.StartAsync(created.DelegationId);

        // Phase 1: Actual, live and open — allotted 30 from the (mocked) creation-time resolution.
        var actual = Assert.Single(started.PhaseTat);
        Assert.Equal(DelegationTaskType.Actual, actual.TaskType);
        Assert.Equal(0, actual.ReviewCycleNumber);
        Assert.Equal(30, actual.AllottedTatMinutes);
        Assert.Null(actual.EndedAt);
        Assert.Equal(30, actual.TatDifferenceMinutes); // live: Used computed fresh (~0 just after Start), so Difference ~= Allotted

        var completed1 = await f.Svc.CompleteAsync(created.DelegationId, null);
        Assert.Equal(2, completed1.PhaseTat.Count);
        var closedActual = completed1.PhaseTat[0];
        Assert.Equal(DelegationTaskType.Actual, closedActual.TaskType);
        Assert.NotNull(closedActual.EndedAt);
        Assert.NotNull(closedActual.TatUsedMinutes);
        Assert.Equal(30 - closedActual.TatUsedMinutes, closedActual.TatDifferenceMinutes);
        var review1 = completed1.PhaseTat[1];
        Assert.Equal(DelegationTaskType.Review, review1.TaskType);
        Assert.Equal(1, review1.ReviewCycleNumber);
        Assert.Equal(15, review1.AllottedTatMinutes);
        Assert.Null(review1.EndedAt);
        // Opened idle — waiting for the reviewer's explicit Start, not live yet.
        Assert.Null(review1.StartedAt);
        Assert.Equal(0, review1.TatUsedMinutes);
        Assert.Equal(15, review1.TatDifferenceMinutes);

        var reviewStarted = await f.Svc.StartReviewAsync(created.DelegationId);
        Assert.NotNull(reviewStarted.PhaseTat[1].StartedAt);

        var reworked = await f.Svc.RequestReworkAsync(created.DelegationId, new RequestTaskReworkRequestDto(), null);
        Assert.Equal(3, reworked.PhaseTat.Count);
        Assert.NotNull(reworked.PhaseTat[1].EndedAt); // review1 closed
        var rework1 = reworked.PhaseTat[2];
        Assert.Equal(DelegationTaskType.Rework, rework1.TaskType);
        Assert.Equal(1, rework1.ReviewCycleNumber);
        Assert.Equal(20, rework1.AllottedTatMinutes);
        Assert.Null(rework1.EndedAt);
        Assert.Null(rework1.StartedAt); // opened idle too

        var reworkStarted = await f.Svc.StartReworkAsync(created.DelegationId);
        Assert.NotNull(reworkStarted.PhaseTat[2].StartedAt);

        var completed2 = await f.Svc.CompleteAsync(created.DelegationId, null);
        Assert.Equal(4, completed2.PhaseTat.Count);
        Assert.NotNull(completed2.PhaseTat[2].EndedAt); // rework1 closed
        var review2 = completed2.PhaseTat[3];
        Assert.Equal(DelegationTaskType.Review, review2.TaskType);
        Assert.Equal(2, review2.ReviewCycleNumber);
        Assert.Equal(15, review2.AllottedTatMinutes);
        Assert.Null(review2.StartedAt);

        await f.Svc.StartReviewAsync(created.DelegationId);
        var approved = await f.Svc.ApproveReviewAsync(created.DelegationId, new ApproveTaskReviewRequestDto(), null);
        Assert.Equal(4, approved.PhaseTat.Count); // Approve closes review2; nothing new opens after it
        Assert.NotNull(approved.PhaseTat[3].EndedAt);
        Assert.Equal(DelegationStatus.Completed, approved.Status);
    }

    [Fact]
    public async Task Review_ClosedWithoutEverBeingStarted_FreezesAZeroUsageSnapshot()
    {
        var f = await NewAsync();
        const string type = "Never Started";
        await AddRuleAsync(f, type, DelegationTaskType.Review, 15);

        var created = await f.Svc.CreateAsync(new DelegationCreateRequestDto { Title = "t", DoerId = "emp-1", DelegationType = type, EndDate = DateTime.UtcNow.AddDays(5) });
        await f.Svc.StartAsync(created.DelegationId);
        var completed = await f.Svc.CompleteAsync(created.DelegationId, null);
        var review = Assert.Single(completed.PhaseTat.Where(p => p.TaskType == DelegationTaskType.Review));
        Assert.Null(review.StartedAt);

        // Approve without ever calling StartReviewAsync — the phase closes with zero elapsed, not an error.
        var approved = await f.Svc.ApproveReviewAsync(created.DelegationId, new ApproveTaskReviewRequestDto(), null);
        var closedReview = approved.PhaseTat.Single(p => p.TaskType == DelegationTaskType.Review);
        Assert.Null(closedReview.StartedAt);
        Assert.NotNull(closedReview.EndedAt);
        Assert.Equal(0, closedReview.TatUsedMinutes);
        Assert.Equal(0, closedReview.PauseCount);
        Assert.Equal(15, closedReview.TatDifferenceMinutes);
    }

    [Fact]
    public async Task StartReview_HappyPath_SetsStartedAtAndBeginsAccrual()
    {
        var f = await NewAsync();
        const string type = "Start Review";
        await AddRuleAsync(f, type, DelegationTaskType.Review, 15);
        var created = await f.Svc.CreateAsync(new DelegationCreateRequestDto { Title = "t", DoerId = "emp-1", DelegationType = type, EndDate = DateTime.UtcNow.AddDays(5) });
        await f.Svc.StartAsync(created.DelegationId);
        await f.Svc.CompleteAsync(created.DelegationId, null);

        var started = await f.Svc.StartReviewAsync(created.DelegationId);
        var review = started.PhaseTat.Single(p => p.TaskType == DelegationTaskType.Review);
        Assert.NotNull(review.StartedAt);
        Assert.Null(review.EndedAt);
    }

    [Fact]
    public async Task StartReview_WhenCurrentOpenPhaseIsRework_Is409()
    {
        var f = await NewAsync();
        var created = await f.Svc.CreateAsync(new DelegationCreateRequestDto { Title = "t", DoerId = "emp-1", DelegationType = "type", EndDate = DateTime.UtcNow.AddDays(5) });
        await f.Svc.StartAsync(created.DelegationId);
        await f.Svc.CompleteAsync(created.DelegationId, null);
        await f.Svc.RequestReworkAsync(created.DelegationId, new RequestTaskReworkRequestDto(), null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.StartReviewAsync(created.DelegationId));
    }

    [Fact]
    public async Task StartRework_AlreadyStarted_Is409()
    {
        var f = await NewAsync();
        var created = await f.Svc.CreateAsync(new DelegationCreateRequestDto { Title = "t", DoerId = "emp-1", DelegationType = "type", EndDate = DateTime.UtcNow.AddDays(5) });
        await f.Svc.StartAsync(created.DelegationId);
        await f.Svc.CompleteAsync(created.DelegationId, null);
        await f.Svc.RequestReworkAsync(created.DelegationId, new RequestTaskReworkRequestDto(), null);
        await f.Svc.StartReworkAsync(created.DelegationId);

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.StartReworkAsync(created.DelegationId));
    }

    [Fact]
    public async Task StartReview_UnknownDelegation_Is404()
    {
        var f = await NewAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => f.Svc.StartReviewAsync(9999));
    }

    [Fact]
    public async Task Pause_WhileCurrentPhaseNotStarted_Is409()
    {
        var f = await NewAsync();
        var created = await f.Svc.CreateAsync(new DelegationCreateRequestDto { Title = "t", DoerId = "emp-1", DelegationType = "type", EndDate = DateTime.UtcNow.AddDays(5) });
        await f.Svc.StartAsync(created.DelegationId);
        await f.Svc.CompleteAsync(created.DelegationId, null); // opens Review idle

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.PauseAsync(created.DelegationId, null));
        Assert.Contains("not been started", ex.Message);

        // Starting the phase first unblocks Pause.
        await f.Svc.StartReviewAsync(created.DelegationId);
        var paused = await f.Svc.PauseAsync(created.DelegationId, null);
        Assert.True(paused.IsPaused);
    }

    [Fact]
    public async Task MultipleOpenHistoricalPhases_RejectTransitionWithoutWritingReview()
    {
        // InMemory intentionally permits the legacy corruption that the new PostgreSQL index prevents.
        var f = await NewAsync();
        var created = await f.Svc.CreateAsync(new DelegationCreateRequestDto { Title = "legacy", DoerId = "emp-1",
            DelegationType = "test", EndDate = DateTime.UtcNow.AddDays(1) });
        await f.Svc.StartAsync(created.DelegationId);
        f.Db.DelegationPhaseTats.Add(new DelegationPhaseTat { DelegationId = created.DelegationId,
            TaskType = "Rework", ReviewCycleNumber = 1, StartedAt = DateTime.UtcNow,
            CreatedBy = "test", CreatedDate = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.SubmitForReviewAsync(created.DelegationId, new()));
        Assert.False(await f.Db.TaskReviews.AnyAsync());
        Assert.Equal(2, await f.Db.DelegationPhaseTats.CountAsync(p => p.EndedAt == null));
    }

    [Fact]
    public async Task NoRuleConfiguredForAPhase_LeavesItUnallotted_ButStillTracksPauseTime()
    {
        var f = await NewAsync();
        const string type = "No Review Rule";
        // Review/Rework deliberately left unconfigured for this delegation type.

        var created = await f.Svc.CreateAsync(new DelegationCreateRequestDto { Title = "t", DoerId = "emp-1", DelegationType = type, EndDate = DateTime.UtcNow.AddDays(5) });
        await f.Svc.StartAsync(created.DelegationId);
        var completed = await f.Svc.CompleteAsync(created.DelegationId, null);

        var review = Assert.Single(completed.PhaseTat.Where(p => p.TaskType == DelegationTaskType.Review));
        Assert.Null(review.AllottedTatMinutes);
        Assert.Null(review.TatUsedMinutes);
        Assert.Null(review.TatDifferenceMinutes);
        Assert.NotNull(review.PauseCount); // still tracked even without a configured budget
    }

    [Fact]
    public async Task AmbiguousRule_ForAPhase_LeavesItUnallotted_RatherThanPickingOneArbitrarily()
    {
        var f = await NewAsync();
        const string type = "Ambiguous Review";
        await AddRuleAsync(f, type, DelegationTaskType.Review, 10);
        await AddRuleAsync(f, type, DelegationTaskType.Review, 20); // two active rules for the same (type, taskType)

        var created = await f.Svc.CreateAsync(new DelegationCreateRequestDto { Title = "t", DoerId = "emp-1", DelegationType = type, EndDate = DateTime.UtcNow.AddDays(5) });
        await f.Svc.StartAsync(created.DelegationId);
        var completed = await f.Svc.CompleteAsync(created.DelegationId, null);

        var review = Assert.Single(completed.PhaseTat.Where(p => p.TaskType == DelegationTaskType.Review));
        Assert.Null(review.AllottedTatMinutes); // ambiguity -> soft "no TAT" for this phase, never a guess
    }
}
