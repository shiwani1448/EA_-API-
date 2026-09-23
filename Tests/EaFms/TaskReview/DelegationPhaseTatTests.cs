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

    private async Task<Fx> NewAsync()
    {
        var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
        var module = new BusinessModule { Name = DelegationService.DelegationBusinessModuleName, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.BusinessModules.Add(module);
        await db.SaveChangesAsync();

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "phase-actor" && u.UserId == 99L);
        var audit = Mock.Of<IAuditService>();
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

        var reworked = await f.Svc.RequestReworkAsync(created.DelegationId, new RequestTaskReworkRequestDto(), null);
        Assert.Equal(3, reworked.PhaseTat.Count);
        Assert.NotNull(reworked.PhaseTat[1].EndedAt); // review1 closed
        var rework1 = reworked.PhaseTat[2];
        Assert.Equal(DelegationTaskType.Rework, rework1.TaskType);
        Assert.Equal(1, rework1.ReviewCycleNumber);
        Assert.Equal(20, rework1.AllottedTatMinutes);
        Assert.Null(rework1.EndedAt);

        var completed2 = await f.Svc.CompleteAsync(created.DelegationId, null);
        Assert.Equal(4, completed2.PhaseTat.Count);
        Assert.NotNull(completed2.PhaseTat[2].EndedAt); // rework1 closed
        var review2 = completed2.PhaseTat[3];
        Assert.Equal(DelegationTaskType.Review, review2.TaskType);
        Assert.Equal(2, review2.ReviewCycleNumber);
        Assert.Equal(15, review2.AllottedTatMinutes);

        var approved = await f.Svc.ApproveReviewAsync(created.DelegationId, new ApproveTaskReviewRequestDto(), null);
        Assert.Equal(4, approved.PhaseTat.Count); // Approve closes review2; nothing new opens after it
        Assert.NotNull(approved.PhaseTat[3].EndedAt);
        Assert.Equal(DelegationStatus.Completed, approved.Status);
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
