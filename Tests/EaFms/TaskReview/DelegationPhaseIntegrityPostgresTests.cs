using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Jarvis5.Tests.EaFms.Delegation;
using Jarvis5.Validators;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using Xunit;

namespace Jarvis5.Tests.EaFms.TaskReview;

// Real services and migrations, isolated PostgreSQL database; never writes developer data.
public class DelegationPhaseIntegrityPostgresTests : IClassFixture<ScratchTatDatabase>
{
    private readonly ScratchTatDatabase _fx;
    private static readonly ICurrentUserService User = Mock.Of<ICurrentUserService>(u => u.UserName == "phase-test" && u.UserId == 7L);
    public DelegationPhaseIntegrityPostgresTests(ScratchTatDatabase fx) => _fx = fx;

    private DelegationService Service(EaFmsDbContext db)
    {
        var audit = new AuditService(db, User);
        var rules = new TatRuleRepository(db);
        return new DelegationService(db, User, audit, new DelegationRepository(db),
            new EaTaskService(db, new EaTaskRepository(db), rules, new CreateEaTaskDtoValidator(), User, audit),
            Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == Path.GetTempPath()),
            new TaskReviewService(db, new TaskReviewRepository(db), User, audit), rules);
    }

    private async Task<long> CreateAsync(bool start = true)
    {
        await using var db = _fx.Db();
        var type = "Integrity-" + Guid.NewGuid().ToString("N");
        foreach (var (taskType, minutes) in new[] { ("Actual", 30), ("Review", 10), ("Rework", 20) })
            db.TatRules.Add(new TatRule { BusinessModuleId = _fx.DelegationModuleId, ModuleName = "Delegation",
                Type = type, TaskType = taskType, TatMinutes = minutes, IsActive = true,
                CreatedBy = "test", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var svc = Service(db);
        var d = await svc.CreateAsync(new DelegationCreateRequestDto { Title = "Phase integrity", DoerId = "test",
            DelegationType = type, EndDate = DateTime.UtcNow.AddDays(1) });
        if (start) await svc.StartAsync(d.DelegationId);
        return d.DelegationId;
    }

    private async Task<DelegationResponseDto> Act(long id, string action)
    {
        await using var db = _fx.Db();
        var svc = Service(db);
        return action switch
        {
            "submit" => await svc.SubmitForReviewAsync(id, new SubmitForReviewRequestDto { SubmittedById = "test", ReviewerId = "reviewer" }),
            "complete" => await svc.CompleteAsync(id, null),
            "rework" => await svc.RequestReworkAsync(id, new RequestTaskReworkRequestDto(), null),
            "approve" => await svc.ApproveReviewAsync(id, new ApproveTaskReviewRequestDto(), null),
            "pause" => await svc.PauseAsync(id, new DelegationPauseRequestDto()),
            "resume" => await svc.ResumeAsync(id),
            _ => throw new ArgumentException(action)
        };
    }

    [Fact]
    public async Task ThreeReviews_TwoReworks_IndependentSnapshotsAndFreshStarts()
    {
        var id = await CreateAsync();
        // Give Actual measurable elapsed time without delaying the suite.
        await using (var db = _fx.Db())
        {
            var phase = await db.DelegationPhaseTats.SingleAsync(p => p.DelegationId == id);
            phase.StartedAt = DateTime.UtcNow.AddMinutes(-7);
            await db.SaveChangesAsync();
        }
        var first = await Act(id, "submit");
        Assert.Equal("reviewer", first.ReviewSummary.ReviewerId);
        Assert.Equal(7, first.PhaseTat[0].TatUsedMinutes);
        Assert.Equal(0, first.PhaseTat[1].TatUsedMinutes);
        var frozenActual = first.PhaseTat[0];
        await Act(id, "rework");
        await Act(id, "complete");
        await Act(id, "rework");
        await Act(id, "submit");
        var done = await Act(id, "approve");
        Assert.Equal("Completed", done.Status);
        Assert.Equal(new[] { "Actual", "Review", "Rework", "Review", "Rework", "Review" }, done.PhaseTat.Select(p => p.TaskType));
        Assert.Equal(new[] { 0, 1, 1, 2, 2, 3 }, done.PhaseTat.Select(p => p.ReviewCycleNumber));
        Assert.Equal(new int?[] { 30, 10, 20, 10, 20, 10 }, done.PhaseTat.Select(p => p.AllottedTatMinutes));
        Assert.Equal(frozenActual.EndedAt, done.PhaseTat[0].EndedAt);
        Assert.Equal(frozenActual.TatUsedMinutes, done.PhaseTat[0].TatUsedMinutes);
        Assert.All(done.PhaseTat.Skip(1), p => Assert.Equal(0, p.TatUsedMinutes));
        for (var i = 1; i < done.PhaseTat.Count; i++)
            Assert.Equal(done.PhaseTat[i - 1].EndedAt, done.PhaseTat[i].StartedAt);
        await using var verify = _fx.Db();
        var rows = await verify.DelegationPhaseTats.Where(p => p.DelegationId == id).OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(6, rows.Count);
        Assert.All(rows, p => { Assert.NotNull(p.EndedAt); Assert.NotNull(p.TatUsedMinutes); Assert.NotNull(p.PauseCount); });
        Assert.Equal(rows.Select(p => p.TatUsedMinutes), done.PhaseTat.Select(p => p.TatUsedMinutes));
        var read = await Service(verify).GetByIdAsync(id);
        Assert.Equal(done.PhaseTat.Select(p => p.TatUsedMinutes), read.PhaseTat.Select(p => p.TatUsedMinutes));
    }

    [Fact]
    public async Task ReviewPrecision_PersistedSnapshotSurvivesReloadAndPauseChanges()
    {
        var id = await CreateAsync();
        await Act(id, "submit");
        await Act(id, "pause");
        await Act(id, "resume");
        await Act(id, "pause");
        await Act(id, "resume");
        await using (var db = _fx.Db())
        {
            var phase = await db.DelegationPhaseTats.SingleAsync(p => p.DelegationId == id && p.EndedAt == null);
            var start = DateTime.UtcNow.AddMinutes(-3);
            start = new DateTime(start.Ticks - start.Ticks % 10, DateTimeKind.Utc);
            phase.StartedAt = start;
            var taskId = await db.Delegations.Where(d => d.Id == id).Select(d => d.EaTaskId).SingleAsync();
            var workflowId = await db.Tasks.Where(t => t.Id == taskId).Select(t => t.WorkflowInstanceId).SingleAsync();
            var pauses = await db.WorkPauses.Where(p => p.WorkflowInstanceId == workflowId).OrderBy(p => p.Id).ToListAsync();
            pauses[0].StartAt = start.AddSeconds(10);
            pauses[0].EndAt = pauses[0].StartAt.AddTicks(33997470);
            pauses[1].StartAt = start.AddSeconds(30);
            pauses[1].EndAt = pauses[1].StartAt.AddTicks(184648690);
            await db.SaveChangesAsync();
        }
        await using (var db = _fx.Db())
        {
            var active = (await Service(db).GetByIdAsync(id)).PhaseTat.Last();
            Assert.Equal(21.864616m, active.TatPausedSeconds);
            Assert.Equal(0, active.TatPausedMinutes);
            Assert.Equal(2, active.PauseCount);
            Assert.Equal(600m, active.TatUsedSeconds + active.TatDifferenceSeconds);
        }
        var closed = (await Act(id, "approve")).PhaseTat.Last();
        Assert.Equal(21.864616m, closed.TatPausedSeconds);
        Assert.Equal(600m, closed.TatUsedSeconds + closed.TatDifferenceSeconds);
        Assert.Equal((int)(closed.TatUsedSeconds!.Value / 60m), closed.TatUsedMinutes);
        Assert.Equal(10 - closed.TatUsedMinutes, closed.TatDifferenceMinutes);
        await using (var db = _fx.Db())
        {
            var row = await db.DelegationPhaseTats.SingleAsync(p => p.DelegationId == id && p.TaskType == "Review");
            Assert.Equal(closed.TatUsedSeconds, row.TatUsedSeconds);
            Assert.Equal(closed.TatPausedSeconds, row.TatPausedSeconds);
            // A closed snapshot must never be recomputed from subsequently edited pause history.
            var taskId = await db.Delegations.Where(d => d.Id == id).Select(d => d.EaTaskId).SingleAsync();
            var workflowId = await db.Tasks.Where(t => t.Id == taskId).Select(t => t.WorkflowInstanceId).SingleAsync();
            foreach (var pause in await db.WorkPauses.Where(p => p.WorkflowInstanceId == workflowId).ToListAsync())
                pause.IsDeleted = true;
            await db.SaveChangesAsync();
        }
        await using var verify = _fx.Db();
        var reread = (await Service(verify).GetByIdAsync(id)).PhaseTat.Last();
        Assert.Equal(closed.TatUsedSeconds, reread.TatUsedSeconds);
        Assert.Equal(closed.TatPausedSeconds, reread.TatPausedSeconds);
        Assert.Equal(closed.TatDifferenceSeconds, reread.TatDifferenceSeconds);
        Assert.Equal(closed.PauseCount, reread.PauseCount);
    }

    [Theory]
    [InlineData("submit", false)]
    [InlineData("complete", false)]
    [InlineData("rework", true)]
    [InlineData("approve", true)]
    public async Task OpenPause_BlocksTransitionWithoutChangingHistory(string action, bool review)
    {
        var id = await CreateAsync();
        if (review) await Act(id, "submit");
        await Act(id, "pause");
        await Assert.ThrowsAsync<BusinessRuleException>(() => Act(id, action));
        await using (var db = _fx.Db())
        {
            Assert.Equal(review ? 2 : 1, await db.DelegationPhaseTats.CountAsync(p => p.DelegationId == id));
            Assert.Equal(1, await db.DelegationPhaseTats.CountAsync(p => p.DelegationId == id && p.EndedAt == null));
            var taskId = await db.Delegations.Where(d => d.Id == id).Select(d => d.EaTaskId).SingleAsync();
            Assert.Equal(review ? 1 : 0, await db.TaskReviews.CountAsync(r => r.EaTaskId == taskId));
            if (review) Assert.Equal("PendingReview", (await db.TaskReviews.SingleAsync(r => r.EaTaskId == taskId)).ReviewStatus);
        }
        await Act(id, "resume");
        var result = await Act(id, action);
        Assert.Equal(1, result.PhaseTat[review ? 1 : 0].PauseCount);
        if (action != "approve") Assert.Equal(0, result.PhaseTat.Last().PauseCount);
    }

    [Theory]
    [InlineData("submit", false)]
    [InlineData("complete", false)]
    [InlineData("rework", true)]
    [InlineData("approve", true)]
    public async Task ConcurrentDuplicateTransitions_OnlyOneSucceeds(string action, bool review)
    {
        var id = await CreateAsync();
        if (review) await Act(id, "submit");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Attempt()
        {
            await gate.Task;
            try { await Act(id, action); return true; }
            catch (BusinessRuleException) { return false; }
        }
        var attempts = new[] { Attempt(), Attempt() };
        gate.SetResult();
        Assert.Equal(1, (await Task.WhenAll(attempts)).Count(x => x));
        await using var db = _fx.Db();
        var phases = await db.DelegationPhaseTats.Where(p => p.DelegationId == id).ToListAsync();
        Assert.Equal(action == "approve" ? 0 : 1, phases.Count(p => p.EndedAt == null));
        Assert.Equal(phases.Count, phases.Select(p => (p.TaskType, p.ReviewCycleNumber)).Distinct().Count());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("type")]
    [InlineData("cycle")]
    public async Task InvalidOpenPhase_RejectsSubmissionBeforeReviewWrite(string fault)
    {
        var id = await CreateAsync();
        await using (var db = _fx.Db())
        {
            var phase = await db.DelegationPhaseTats.SingleAsync(p => p.DelegationId == id);
            if (fault == "missing") db.Remove(phase);
            if (fault == "type") phase.TaskType = "Review";
            if (fault == "cycle") phase.ReviewCycleNumber = 4;
            await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<BusinessRuleException>(() => Act(id, "submit"));
        await using var verify = _fx.Db();
        var taskId = await verify.Delegations.Where(d => d.Id == id).Select(d => d.EaTaskId).SingleAsync();
        Assert.False(await verify.TaskReviews.AnyAsync(r => r.EaTaskId == taskId));
    }

    [Fact]
    public async Task NotStarted_SubmissionIsRejected()
    {
        var id = await CreateAsync(false);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Act(id, "submit"));
    }

    [Theory]
    [InlineData("submit", "complete", false)]
    [InlineData("approve", "rework", true)]
    public async Task CompetingTransitions_SerializeWithoutExtraPhases(string first, string second, bool review)
    {
        var id = await CreateAsync();
        if (review) await Act(id, "submit");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Attempt(string action)
        {
            await gate.Task;
            try { await Act(id, action); return true; }
            catch (BusinessRuleException) { return false; }
        }
        var attempts = new[] { Attempt(first), Attempt(second) };
        gate.SetResult();
        Assert.Equal(1, (await Task.WhenAll(attempts)).Count(x => x));
        await using var db = _fx.Db();
        var d = await db.Delegations.SingleAsync(d => d.Id == id);
        Assert.Equal(d.Status == "Completed" ? 0 : 1,
            await db.DelegationPhaseTats.CountAsync(p => p.DelegationId == id && p.EndedAt == null));
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("rework")]
    public async Task WrongReviewCycle_RejectsDecisionWithoutClosingPhase(string action)
    {
        var id = await CreateAsync();
        await Act(id, "submit");
        await using (var db = _fx.Db())
        {
            var phase = await db.DelegationPhaseTats.SingleAsync(p => p.DelegationId == id && p.EndedAt == null);
            phase.ReviewCycleNumber = 9;
            await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<BusinessRuleException>(() => Act(id, action));
        await using var verify = _fx.Db();
        var taskId = await verify.Delegations.Where(d => d.Id == id).Select(d => d.EaTaskId).SingleAsync();
        Assert.Equal("PendingReview", (await verify.TaskReviews.SingleAsync(r => r.EaTaskId == taskId)).ReviewStatus);
        Assert.Equal(1, await verify.DelegationPhaseTats.CountAsync(p => p.DelegationId == id && p.EndedAt == null));
    }

    [Fact]
    public async Task SuccessorInsertFailure_RollsBackReviewAndFrozenSnapshot()
    {
        var id = await CreateAsync();
        await using (var db = _fx.Db())
        {
            // Deliberately inconsistent historical row: the next Review identity already exists.
            db.DelegationPhaseTats.Add(new DelegationPhaseTat { DelegationId = id, TaskType = "Review",
                ReviewCycleNumber = 1, StartedAt = DateTime.UtcNow, EndedAt = DateTime.UtcNow,
                CreatedBy = "test", CreatedDate = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<DbUpdateException>(() => Act(id, "submit"));
        await using var verify = _fx.Db();
        var actual = await verify.DelegationPhaseTats.SingleAsync(p => p.DelegationId == id && p.TaskType == "Actual");
        Assert.Null(actual.EndedAt);
        Assert.Null(actual.TatUsedMinutes);
        var taskId = await verify.Delegations.Where(d => d.Id == id).Select(d => d.EaTaskId).SingleAsync();
        Assert.False(await verify.TaskReviews.AnyAsync(r => r.EaTaskId == taskId));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Database_RejectsDuplicateIdentityOrSecondOpenPhase(bool duplicateIdentity)
    {
        var id = await CreateAsync();
        await using var db = _fx.Db();
        db.DelegationPhaseTats.Add(new DelegationPhaseTat { DelegationId = id,
            TaskType = duplicateIdentity ? "Actual" : "Review", ReviewCycleNumber = duplicateIdentity ? 0 : 1,
            StartedAt = DateTime.UtcNow, EndedAt = duplicateIdentity ? DateTime.UtcNow : null,
            CreatedBy = "test", CreatedDate = DateTime.UtcNow });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState);
    }
}
