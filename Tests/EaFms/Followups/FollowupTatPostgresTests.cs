using System;
using System.Linq;
using System.Threading.Tasks;
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
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Followups;

/// <summary>
/// Follow-up TAT-rule classification and the EaTask backfill, against a real (scratch) PostgreSQL
/// database — TatRuleService's normalisation and the backfill are Postgres SQL. Gated on
/// EA_DELEGATION_TEST_SERVER exactly like DelegationTypeOnlyTatTests.
/// </summary>
public class FollowupTatPostgresTests : IClassFixture<ScratchTatDatabase>
{
    private readonly ScratchTatDatabase _fx;
    private static readonly ICurrentUserService User = Mock.Of<ICurrentUserService>(u => u.UserName == "tat-actor" && u.UserId == 7L);

    public FollowupTatPostgresTests(ScratchTatDatabase fx) => _fx = fx;

    private static string Unique(string prefix = "Type") => $"{prefix}-{Guid.NewGuid():N}";

    private static TatRuleService Rules(EaFmsDbContext db) =>
        new(db, new TatRuleRepository(db), new TatRuleDtoValidator(), User, Mock.Of<IAuditService>());

    private static Task<long> FollowupModuleIdAsync(EaFmsDbContext db) =>
        db.BusinessModules.Where(m => m.Name == "Follow-up").Select(m => m.Id).SingleAsync();

    [Fact]
    public async Task Migration_SeedsTheFollowupModule()
    {
        await using var db = _fx.Db();
        Assert.True(await FollowupModuleIdAsync(db) > 0);
    }

    [Theory]
    [InlineData("Review")]
    [InlineData("Rework")]
    public async Task TatRule_ReviewOrRework_ForFollowup_Is400(string taskType)
    {
        await using var db = _fx.Db();
        var moduleId = await FollowupModuleIdAsync(db);
        var type = Unique();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Rules(db).SaveAsync(null,
            new SaveTatRuleDto { ModuleId = moduleId, Type = type, TaskType = taskType, TatMinutes = 30, IsActive = true }, default));

        Assert.Equal("Follow-up TAT rules only support TaskType Actual.", ex.Message);
        await using var check = _fx.Db();
        Assert.False(await check.TatRules.AnyAsync(r => r.Type == type));
    }

    [Fact]
    public async Task TatRule_Actual_IsAccepted_TypeOnly_AndASecondActiveRuleIsRejected()
    {
        var type = Unique();
        long moduleId;
        await using (var db = _fx.Db())
        {
            moduleId = await FollowupModuleIdAsync(db);
            var saved = await Rules(db).SaveAsync(null, new SaveTatRuleDto { ModuleId = moduleId, Type = type, TaskType = DelegationTaskType.Actual, TatMinutes = 45, IsActive = true }, default);
            Assert.Equal((type, (string?)null, DelegationTaskType.Actual), (saved.Type, saved.Subtype, saved.TaskType));
        }
        await using (var db = _fx.Db())
            await Assert.ThrowsAsync<BadRequestException>(() => Rules(db).SaveAsync(null,
                new SaveTatRuleDto { ModuleId = moduleId, Type = Unique(), Subtype = "General", TaskType = DelegationTaskType.Actual, TatMinutes = 5, IsActive = true }, default));
        await using (var db = _fx.Db())
            await Assert.ThrowsAsync<BusinessRuleException>(() => Rules(db).SaveAsync(null,
                new SaveTatRuleDto { ModuleId = moduleId, Type = type.ToUpperInvariant(), TaskType = DelegationTaskType.Actual, TatMinutes = 10, IsActive = true }, default));

        // The DB's partial unique index is the concurrent-save backstop; it now covers Follow-up too.
        await using var raw = _fx.Db();
        raw.TatRules.Add(new TatRule { BusinessModuleId = moduleId, ModuleName = "Follow-up", Type = type, TaskType = DelegationTaskType.Actual,
            TatMinutes = 11, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => raw.SaveChangesAsync());
    }

    /// <summary>The backfill migration's own SQL (read via reflection, so the test replays exactly what shipped).</summary>
    private static string[] BackfillSql()
    {
        var type = typeof(EaFmsDbContext).Assembly.GetType("Studio5JarvisMasterApi.Migrations.BackfillFollowupEaTasks", throwOnError: true)!;
        var migration = Activator.CreateInstance(type)!;
        var ops = (System.Collections.IEnumerable)type.GetProperty("UpOperations")!.GetValue(migration)!;
        return ops.Cast<object>().Select(o => (string?)o.GetType().GetProperty("Sql")?.GetValue(o)).OfType<string>().ToArray();
    }

    [Fact]
    public async Task Backfill_OpenGetNotStartedWithRuleTat_CompletedCopyCompletedAt_NoPhaseRows_Idempotent()
    {
        var type = Unique("Backfill");
        var completedAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        long openId, noRuleId, doneId;
        await using (var db = _fx.Db())
        {
            var moduleId = await FollowupModuleIdAsync(db);
            db.TatRules.Add(new TatRule { BusinessModuleId = moduleId, ModuleName = "Follow-up", Type = type, TaskType = DelegationTaskType.Actual,
                TatMinutes = 240, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
            Followup F(string? t, DateTime? done) => new() { Subject = "Legacy", Type = t, DueAt = DateTime.UtcNow, CompletedAt = done, CreatedBy = "legacy", CreatedDate = DateTime.UtcNow };
            var open = F(type, null); var noRule = F(Unique("NoRule"), null); var done = F(type, completedAt);
            db.Followups.AddRange(open, noRule, done);
            await db.SaveChangesAsync();
            (openId, noRuleId, doneId) = (open.Id, noRule.Id, done.Id);
        }

        async Task ReplayAsync()
        {
            var statements = BackfillSql();
            Assert.Equal(2, statements.Length);
            await using var db = _fx.Db();
            foreach (var sql in statements)
                await db.Database.ExecuteSqlRawAsync(sql);
        }
        await ReplayAsync();
        await ReplayAsync(); // second run adds nothing

        await using var check = _fx.Db();
        var ids = new[] { openId, noRuleId, doneId };
        var rows = await check.Followups.AsNoTracking().Where(f => ids.Contains(f.Id)).ToDictionaryAsync(f => f.Id);
        var tasks = await check.Tasks.AsNoTracking().Where(t => t.ModuleName == "Follow-up" && ids.Select(i => i.ToString()).Contains(t.BusinessRecordId!)).ToListAsync();
        Assert.Equal(3, tasks.Count);
        EaTask TaskFor(long id) => tasks.Single(t => t.Id == rows[id].EaTaskId);

        Assert.Equal((EaTaskExecutionStatus.NotStarted, (int?)240), (TaskFor(openId).ExecutionStatus, TaskFor(openId).AllottedTatMinutes));
        Assert.NotNull(TaskFor(openId).TatRuleId);
        Assert.Equal((EaTaskExecutionStatus.NotStarted, (int?)null, (long?)null), (TaskFor(noRuleId).ExecutionStatus, TaskFor(noRuleId).AllottedTatMinutes, TaskFor(noRuleId).TatRuleId));
        Assert.Equal((EaTaskExecutionStatus.Completed, (DateTime?)completedAt, (int?)null), (TaskFor(doneId).ExecutionStatus, TaskFor(doneId).CompletedAt, TaskFor(doneId).AllottedTatMinutes));
        Assert.All(tasks, t => Assert.Equal("system-backfill", t.CreatedBy));
        Assert.False(await check.FollowupPhaseTats.AnyAsync(p => ids.Contains(p.FollowupId)));
    }
}
