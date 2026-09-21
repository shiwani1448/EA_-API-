using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
using Jarvis5.Validators;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using UglyToad.PdfPig.Writer;
using Xunit;
using DelegationEntity = Jarvis5.Entities.EaFms.Delegation;

namespace Jarvis5.Tests.EaFms.Delegation;

/// <summary>
/// A throwaway PostgreSQL database (created from the real migrations, dropped afterwards). Type-only TAT resolution uses
/// Postgres-only SQL (FOR SHARE, advisory locks, lower(btrim())), so it cannot run on EF InMemory — and using a scratch
/// database means none of these tests writes a single row into the developer database.
/// </summary>
public sealed class ScratchTatDatabase : IAsyncLifetime
{
    private const string Server = "Host=localhost;Port=5432;Username=postgres;Password=123456";
    private readonly string _name = "scratch_deleg_tat_" + Guid.NewGuid().ToString("N")[..12];
    public string Connection => $"{Server};Database={_name}";
    public long DelegationModuleId { get; private set; }
    public long MeetingModuleId { get; private set; }

    public EaFmsDbContext Db() => new(new DbContextOptionsBuilder<EaFmsDbContext>().UseNpgsql(Connection).Options);

    public async Task InitializeAsync()
    {
        await using (var admin = new NpgsqlConnection($"{Server};Database=postgres"))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_name}\"", admin);
            await create.ExecuteNonQueryAsync();
        }
        await using var db = Db();
        await db.Database.MigrateAsync();
        foreach (var name in new[] { "Delegation", "Meeting" })
            if (!await db.BusinessModules.AnyAsync(m => m.Name == name))
                db.BusinessModules.Add(new BusinessModule { Name = name, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        foreach (var name in new[] { "Captured", "In Progress", "Completed" })
            if (!await db.Statuses.AnyAsync(s => s.Name == name))
                db.Statuses.Add(new Status { Name = name, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
        DelegationModuleId = await db.BusinessModules.Where(m => m.Name == "Delegation").Select(m => m.Id).SingleAsync();
        MeetingModuleId = await db.BusinessModules.Where(m => m.Name == "Meeting").Select(m => m.Id).SingleAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection($"{Server};Database=postgres");
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// Delegation TAT: the rule identity is Delegation module + Type (= delegationType), with no subtype. Everything below
/// runs the real EaTaskService / DelegationService / TatRuleService / EmReportService against the scratch database.
/// </summary>
public class DelegationTypeOnlyTatTests : IClassFixture<ScratchTatDatabase>, IDisposable
{
    private readonly ScratchTatDatabase _fx;
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private static readonly ICurrentUserService User = Mock.Of<ICurrentUserService>(u => u.UserName == "tat-actor" && u.UserId == 7L);

    public DelegationTypeOnlyTatTests(ScratchTatDatabase fx)
    {
        _fx = fx;
        Directory.CreateDirectory(_root);
    }

    public void Dispose() { try { Directory.Delete(_root, true); } catch { /* best-effort */ } }

    private static string Unique(string prefix = "Type") => $"{prefix}-{Guid.NewGuid():N}";

    private DelegationService NewService(EaFmsDbContext db)
    {
        var audit = new AuditService(db, User);
        var eaTasks = new EaTaskService(db, new EaTaskRepository(db), new TatRuleRepository(db), new CreateEaTaskDtoValidator(), User, audit);
        return new DelegationService(db, User, audit, new DelegationRepository(db), eaTasks,
            Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(e => e.ContentRootPath == _root));
    }

    private async Task<TatRule> AddRuleAsync(string? type, string? subtype, int minutes, bool active = true, bool deleted = false, long? moduleId = null)
    {
        await using var db = _fx.Db();
        var id = moduleId ?? _fx.DelegationModuleId;
        var rule = new TatRule
        {
            BusinessModuleId = id, ModuleName = id == _fx.MeetingModuleId ? "Meeting" : "Delegation", Type = type, Subtype = subtype,
            TatMinutes = minutes, IsActive = active, IsDeleted = deleted, CreatedBy = "seed", CreatedDate = DateTime.UtcNow
        };
        db.TatRules.Add(rule);
        await db.SaveChangesAsync();
        return rule;
    }

    private async Task<DelegationResponseDto> CreateAsync(string? type, DateTime? endDate = null, DateTime? startDate = null)
    {
        await using var db = _fx.Db();
        return await NewService(db).CreateAsync(new DelegationCreateRequestDto
        {
            Title = "TAT " + Guid.NewGuid().ToString("N")[..8], DoerId = "EMP-TAT", DoerNameSnapshot = "Tat Doer",
            DelegationType = type, EndDate = endDate, StartDate = startDate
        });
    }

    private async Task<EaTask> TaskOf(long eaTaskId)
    {
        await using var db = _fx.Db();
        return await db.Tasks.AsNoTracking().SingleAsync(t => t.Id == eaTaskId);
    }

    private async Task<DelegationResponseDto> Run(Func<DelegationService, Task<DelegationResponseDto>> action)
    {
        await using var db = _fx.Db();
        return await action(NewService(db));
    }

    private static IFormFile Pdf()
    {
        var b = new PdfDocumentBuilder();
        b.AddPage(200, 200);
        var ms = new MemoryStream(b.Build());
        return new FormFile(ms, 0, ms.Length, "completionPdf", "done.pdf") { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
    }

    /// <summary>Moves the actual execution start into the past so elapsed time is deterministic without sleeping.</summary>
    private async Task BackdateStartAsync(long delegationId, long eaTaskId, int minutesAgo)
    {
        await using var db = _fx.Db();
        var at = DateTime.UtcNow.AddMinutes(-minutesAgo);
        (await db.Delegations.SingleAsync(d => d.Id == delegationId)).StartedAt = at;
        (await db.Tasks.SingleAsync(t => t.Id == eaTaskId)).StartedAt = at;
        await db.SaveChangesAsync();
    }

    private async Task SetPauseWindowAsync(long eaTaskId, int startOffsetMinutes, int? endOffsetMinutes)
    {
        await using var db = _fx.Db();
        var task = await db.Tasks.SingleAsync(t => t.Id == eaTaskId);
        var pause = await db.WorkPauses.SingleAsync(p => p.WorkflowInstanceId == task.WorkflowInstanceId);
        pause.StartAt = task.StartedAt!.Value.AddMinutes(startOffsetMinutes);
        pause.EndAt = endOffsetMinutes.HasValue ? task.StartedAt.Value.AddMinutes(endOffsetMinutes.Value) : null;
        await db.SaveChangesAsync();
    }

    // ============================================================
    // CREATE — Type-only TAT resolution
    // ============================================================
    [Fact]
    public async Task Create_SelfDelegation_MapsToTypeSelfDelegation_WithNullSubtype_AndSnapshotsTheRule()
    {
        var type = Unique("Self Delegation");   // any frontend value works; the suffix keeps tests independent
        var rule = await AddRuleAsync(type, null, 90);

        var created = await CreateAsync(type);

        var task = await TaskOf(created.EaTaskId);
        Assert.Equal(type, task.Type);
        Assert.Null(task.Subtype);
        Assert.Equal(rule.Id, task.TatRuleId);
        Assert.Equal(90, task.AllottedTatMinutes);
        Assert.Null(task.TatUsedMinutes);
        Assert.Equal(EaTaskExecutionStatus.NotStarted, task.ExecutionStatus);
        Assert.Equal(_fx.DelegationModuleId, task.BusinessModuleId);   // resolved by name, never a hardcoded id
        Assert.Equal(type, created.DelegationType);
    }

    [Fact]
    public async Task Create_DirectorDelegation_MapsToItsOwnTypeRule_AndNeverToAnotherTypesRule()
    {
        var self = Unique("Self Delegation");
        var director = Unique("Director Delegation");
        var selfRule = await AddRuleAsync(self, null, 60);
        var directorRule = await AddRuleAsync(director, null, 120);

        var created = await CreateAsync(director);

        var task = await TaskOf(created.EaTaskId);
        Assert.Equal(director, task.Type);
        Assert.Equal(directorRule.Id, task.TatRuleId);
        Assert.Equal(120, task.AllottedTatMinutes);
        Assert.NotEqual(selfRule.Id, task.TatRuleId);
    }

    [Fact]
    public async Task Create_ANewFrontendType_WorksWithoutAnyBackendChange_WhenItsRuleIsConfigured()
    {
        var type = Unique("Management Delegation");

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(type));   // not configured yet
        var rule = await AddRuleAsync(type, null, 45);
        var created = await CreateAsync(type);

        Assert.Equal(rule.Id, (await TaskOf(created.EaTaskId)).TatRuleId);
    }

    [Fact]
    public async Task Create_MissingTypeRule_FailsWithTheCanonicalTatFailure_AndCreatesNothing()
    {
        var type = Unique("Unconfigured");
        await using var before = _fx.Db();
        var (delegations, tasks) = (await before.Delegations.CountAsync(), await before.Tasks.CountAsync());

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(type));

        Assert.Contains("No active TAT rule", ex.Message);
        await using var after = _fx.Db();
        Assert.Equal((delegations, tasks), (await after.Delegations.CountAsync(), await after.Tasks.CountAsync()));
    }

    [Fact]
    public async Task Create_RuleLookup_IsCaseAndWhitespaceInsensitive_LikeTheExistingExactLookup()
    {
        var type = Unique("Case Type");
        var rule = await AddRuleAsync(type, null, 30);

        var created = await CreateAsync($"  {type.ToUpperInvariant()} ");

        Assert.Equal(rule.Id, (await TaskOf(created.EaTaskId)).TatRuleId);
    }

    [Fact]
    public async Task Create_ARuleThatCarriesASubtype_IsNotATypeOnlyMatch_AndNoGeneralSubtypeIsEverGenerated()
    {
        var type = Unique("Has Subtype");
        await AddRuleAsync(type, "General", 30);   // a typed+subtyped rule must not satisfy Delegation

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(type));
        await using var db = _fx.Db();
        Assert.DoesNotContain(await db.Tasks.Where(t => t.Subtype != null && t.ModuleName == "Delegation").ToListAsync(), _ => true);
        Assert.False(await db.Tasks.AnyAsync(t => t.ModuleName == "Delegation" && t.Subtype == "General"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task Create_InactiveOrDeletedRules_AreIgnored(bool active, bool deleted)
    {
        var type = Unique("Ignored");
        await AddRuleAsync(type, null, 30, active: active, deleted: deleted);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(type));
    }

    [Fact]
    public async Task Create_TwoActiveRulesForTheSameType_IsRejectedAsAmbiguous()
    {
        var type = Unique("Dup");
        await AddRuleAsync(type, null, 30);
        await AddRuleAsync(type, null, 40);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(type));

        Assert.Contains("Multiple active TAT rules", ex.Message);
    }

    [Fact]
    public async Task Create_ZeroOrNegativeRuleMinutes_AreImpossible_ByTheExistingCheckConstraint()
    {
        await Assert.ThrowsAsync<DbUpdateException>(() => AddRuleAsync(Unique("Zero"), null, 0));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithoutDelegationType_StaysNoTat_EvenWhenRulesExist(string? type)
    {
        await AddRuleAsync(Unique("Some Rule"), null, 30);

        var created = await CreateAsync(type);

        var task = await TaskOf(created.EaTaskId);
        Assert.Null(task.Type);
        Assert.Null(task.Subtype);
        Assert.Null(task.TatRuleId);
        Assert.Null(task.AllottedTatMinutes);
        Assert.Null(task.TatUsedMinutes);
        Assert.Null(created.DelegationType);
    }

    [Fact]
    public async Task MeetingCreatedDelegation_HasNoType_AndStaysNoTat_NeverDependingOnATatRule()
    {
        await using var db = _fx.Db();
        var created = await NewService(db).CreateCoreAsync(new DelegationCreateCommand
        {
            Title = "From meeting", DoerId = "EMP-9", DoerNameSnapshot = "Owner",
            SourceBusinessModuleId = _fx.MeetingModuleId, SourceEntityId = "12345", SourceReference = "MTG-9"
        }, default);

        var task = await TaskOf(created.EaTaskId);
        Assert.Null(created.DelegationType);
        Assert.Null(task.Type);
        Assert.Null(task.Subtype);
        Assert.Null(task.TatRuleId);
        Assert.Null(task.AllottedTatMinutes);
    }

    // ============================================================
    // Shared engine — Meeting/generic behaviour unchanged
    // ============================================================
    [Fact]
    public async Task GenericCreate_StillRequiresTypeAndSubtype_TheExactMeetingStyleLookupIsUnchanged()
    {
        await using var db = _fx.Db();
        var eaTasks = new EaTaskService(db, new EaTaskRepository(db), new TatRuleRepository(db), new CreateEaTaskDtoValidator(), User, new AuditService(db, User));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => eaTasks.CreateAsync(
            new CreateEaTaskDto { ModuleId = _fx.DelegationModuleId, BusinessRecordId = Unique("REC"), Task = "t", Type = "Only Type" }, default));

        Assert.Contains("Type and subtype are required", ex.Message);
    }

    [Fact]
    public async Task ExactLookup_DoesNotMatchTypeOnlyRules_AndTypeOnlyLookupDoesNotMatchSubtypedRules()
    {
        var type = Unique("Split");
        var typeOnly = await AddRuleAsync(type, null, 10, moduleId: _fx.MeetingModuleId);
        var exact = await AddRuleAsync(type, "Review", 20, moduleId: _fx.MeetingModuleId);
        await using var db = _fx.Db();
        var repo = new TatRuleRepository(db);

        var exactMatch = Assert.Single(await repo.GetApplicableAsync(_fx.MeetingModuleId, type, "Review", default));
        var typeOnlyMatch = Assert.Single(await repo.GetApplicableByTypeOnlyAsync(_fx.MeetingModuleId, type, default));

        Assert.Equal(exact.Id, exactMatch.Id);
        Assert.Equal(typeOnly.Id, typeOnlyMatch.Id);
        Assert.Empty(await repo.GetApplicableAsync(_fx.MeetingModuleId, type, "", default));
    }

    [Fact]
    public async Task TypeOnlyCreation_IsRefusedForAnyOtherModule()
    {
        await using var db = _fx.Db();
        var eaTasks = new EaTaskService(db, new EaTaskRepository(db), new TatRuleRepository(db), new CreateEaTaskDtoValidator(), User, new AuditService(db, User));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => eaTasks.CreateWithTypeOnlyTatAsync(
            new CreateEaTaskDto { ModuleId = _fx.MeetingModuleId, BusinessRecordId = Unique("REC"), Task = "t", Type = "Anything" }, default));

        Assert.Contains("only supported for Delegation", ex.Message);
    }

    // ============================================================
    // TAT rule configuration API
    // ============================================================
    private TatRuleService NewRuleService(EaFmsDbContext db) =>
        new(db, new TatRuleRepository(db), new TatRuleDtoValidator(), User, new AuditService(db, User));

    [Fact]
    public async Task TatRuleApi_AcceptsADelegationRuleWithModuleTypeAndMinutesOnly()
    {
        var type = Unique("Configured");
        await using var db = _fx.Db();

        var saved = await NewRuleService(db).SaveAsync(null,
            new SaveTatRuleDto { ModuleId = _fx.DelegationModuleId, Type = type, TatMinutes = 75, IsActive = true }, default);

        Assert.Equal(type, saved.Type);
        Assert.Null(saved.Subtype);
        Assert.Equal(75, saved.TatMinutes);
        // and the new rule is exactly what Delegation creation resolves
        var created = await CreateAsync(type);
        Assert.Equal(saved.Id, (await TaskOf(created.EaTaskId)).TatRuleId);
    }

    [Fact]
    public async Task TatRuleApi_RejectsASecondActiveTypeOnlyRule_AndADelegationRuleWithASubtype()
    {
        var type = Unique("Configured");
        await using (var db = _fx.Db())
            await NewRuleService(db).SaveAsync(null, new SaveTatRuleDto { ModuleId = _fx.DelegationModuleId, Type = type.ToUpperInvariant(), TatMinutes = 10, IsActive = true }, default);

        await using var db2 = _fx.Db();
        await Assert.ThrowsAsync<BusinessRuleException>(() => NewRuleService(db2).SaveAsync(null,
            new SaveTatRuleDto { ModuleId = _fx.DelegationModuleId, Type = type, TatMinutes = 20, IsActive = true }, default));
        await using var db3 = _fx.Db();
        await Assert.ThrowsAsync<BadRequestException>(() => NewRuleService(db3).SaveAsync(null,
            new SaveTatRuleDto { ModuleId = _fx.DelegationModuleId, Type = Unique(), Subtype = "General", TatMinutes = 20, IsActive = true }, default));
    }

    [Fact]
    public async Task TatRuleApi_MeetingRules_StillRequireASubtype_AndStillAcceptTypePlusSubtype()
    {
        await using var db = _fx.Db();
        await Assert.ThrowsAsync<BadRequestException>(() => NewRuleService(db).SaveAsync(null,
            new SaveTatRuleDto { ModuleId = _fx.MeetingModuleId, Type = Unique(), TatMinutes = 20, IsActive = true }, default));

        await using var db2 = _fx.Db();
        var saved = await NewRuleService(db2).SaveAsync(null,
            new SaveTatRuleDto { ModuleId = _fx.MeetingModuleId, Type = Unique(), Subtype = "Review", TatMinutes = 20, IsActive = true }, default);
        Assert.Equal("Review", saved.Subtype);
    }

    // ============================================================
    // LIFECYCLE — Start / Pause / Resume / Complete
    // ============================================================
    [Fact]
    public async Task Start_TheActualStartedAt_StartsTheClock_NotThePlannedStartDate()
    {
        var type = Unique("Clock");
        await AddRuleAsync(type, null, 240);
        var future = DateTime.UtcNow.AddDays(10);
        var created = await CreateAsync(type, startDate: future);

        var started = await Run(s => s.StartAsync(created.DelegationId));

        var task = await TaskOf(created.EaTaskId);
        Assert.Equal(EaTaskExecutionStatus.InProgress, task.ExecutionStatus);
        Assert.True(Math.Abs((started.StartedAt!.Value - task.StartedAt!.Value).TotalMilliseconds) < 1);   // Postgres stores microseconds
        Assert.True(Math.Abs((future - started.StartDate!.Value).TotalMilliseconds) < 1);
        await using var db = _fx.Db();
        var view = await new EaTaskService(db, new EaTaskRepository(db), new TatRuleRepository(db), new CreateEaTaskDtoValidator(), User, new AuditService(db, User))
            .GetAsync(created.EaTaskId, default);
        Assert.Equal(240, view.AllottedTatMinutes);
        Assert.InRange(view.CurrentTatUsedMinutes!.Value, 0, 1);   // counted from StartedAt, not from a start date ten days away
    }

    [Fact]
    public async Task PauseAndResume_ExcludePausedTime_FromActiveTat_LiveAndAtCompletion()
    {
        var type = Unique("Pausing");
        await AddRuleAsync(type, null, 240);
        var created = await CreateAsync(type);
        await Run(s => s.StartAsync(created.DelegationId));
        await BackdateStartAsync(created.DelegationId, created.EaTaskId, 180);
        await Run(s => s.PauseAsync(created.DelegationId, null));

        // Paused from minute 30 and still open: the clock is frozen at 30 active minutes.
        await SetPauseWindowAsync(created.EaTaskId, 30, null);
        await using (var db = _fx.Db())
        {
            var live = await new EaTaskService(db, new EaTaskRepository(db), new TatRuleRepository(db), new CreateEaTaskDtoValidator(), User, new AuditService(db, User))
                .GetAsync(created.EaTaskId, default);
            Assert.Equal(EaTaskExecutionStatus.InProgress, live.ExecutionStatus);   // never persisted as Paused
            Assert.True(live.IsPaused);
            Assert.Equal(30, live.CurrentTatUsedMinutes);
        }

        // Resume: the pause is closed and the clock continues (a 60-minute pause is excluded from 180 elapsed).
        await Run(s => s.ResumeAsync(created.DelegationId));
        await SetPauseWindowAsync(created.EaTaskId, 30, 90);
        await using (var db = _fx.Db())
        {
            var live = await new EaTaskService(db, new EaTaskRepository(db), new TatRuleRepository(db), new CreateEaTaskDtoValidator(), User, new AuditService(db, User))
                .GetAsync(created.EaTaskId, default);
            Assert.False(live.IsPaused);
            Assert.Equal(120, live.CurrentTatUsedMinutes);
        }

        var completed = await Run(s => s.CompleteAsync(created.DelegationId, Pdf()));

        var task = await TaskOf(created.EaTaskId);
        Assert.Equal(EaTaskExecutionStatus.Completed, task.ExecutionStatus);
        Assert.Equal(120, task.TatUsedMinutes);   // frozen: 180 elapsed - 60 paused
        Assert.NotNull(completed.CompletionPdfAttachmentId);   // completionPdf behaviour unchanged
        await using var verify = _fx.Db();
        Assert.False(await verify.WorkPauses.AnyAsync(p => p.WorkflowInstanceId == task.WorkflowInstanceId && p.EndAt == null));
    }

    [Fact]
    public async Task Complete_WithoutAnyPause_FreezesElapsedTime_AndCompleteWhilePausedIsStillBlocked()
    {
        var type = Unique("NoPause");
        await AddRuleAsync(type, null, 240);
        var created = await CreateAsync(type);
        await Run(s => s.StartAsync(created.DelegationId));
        await BackdateStartAsync(created.DelegationId, created.EaTaskId, 100);

        await Run(s => s.PauseAsync(created.DelegationId, null));
        var blocked = await Assert.ThrowsAsync<BusinessRuleException>(() => Run(s => s.CompleteAsync(created.DelegationId, null)));
        Assert.Equal("Resume or continue open pauses/waiting before completion.", blocked.Message);
        Assert.Null((await TaskOf(created.EaTaskId)).TatUsedMinutes);

        await Run(s => s.ResumeAsync(created.DelegationId));
        await SetPauseWindowAsync(created.EaTaskId, 100, 100);   // zero-length pause: nothing to exclude
        await Run(s => s.CompleteAsync(created.DelegationId, null));

        Assert.Equal(100, (await TaskOf(created.EaTaskId)).TatUsedMinutes);
    }

    [Fact]
    public async Task Complete_NoTatDelegation_LeavesTatUsedNull_AndStillWorks()
    {
        var created = await CreateAsync(null);
        await Run(s => s.StartAsync(created.DelegationId));
        await BackdateStartAsync(created.DelegationId, created.EaTaskId, 50);

        var completed = await Run(s => s.CompleteAsync(created.DelegationId, Pdf()));

        var task = await TaskOf(created.EaTaskId);
        Assert.Equal(DelegationStatus.Completed, completed.Status);
        Assert.Null(task.AllottedTatMinutes);
        Assert.Null(task.TatUsedMinutes);
        Assert.NotNull(completed.CompletionPdfAttachmentId);
    }

    [Fact]
    public void SharedActiveTatCalculation_IsTheSameMathMeetingUses()
    {
        var start = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
        var pauses = new[]
        {
            new WorkPause { StartAt = start.AddMinutes(10), EndAt = start.AddMinutes(40), CreatedBy = "t" },
            new WorkPause { StartAt = start.AddMinutes(30), EndAt = start.AddMinutes(60), CreatedBy = "t" },   // overlaps: merged
            new WorkPause { StartAt = start.AddMinutes(500), EndAt = start.AddMinutes(600), CreatedBy = "t" }, // after the end: ignored
        };

        Assert.Equal(90 - 50, EaTaskService.CalculateActiveTatMinutes(start, start.AddMinutes(90), pauses));
        Assert.Equal(0, EaTaskService.CalculateActiveTatMinutes(start, start.AddMinutes(-5), pauses));   // never negative
        var task = new EaTask { AllottedTatMinutes = 100, StartedAt = start, ExecutionStatus = EaTaskExecutionStatus.InProgress };
        Assert.Equal(EaTaskService.CalculateActiveTatMinutes(start, start.AddMinutes(90), pauses),
            EaTaskService.CalculateCurrentTatUsedMinutes(task, pauses, start.AddMinutes(90)));
    }

    // ============================================================
    // EM REPORT / TASK REGISTER
    // ============================================================
    private async Task<EmReportTaskRowDto> RegisterRow(DelegationResponseDto created)
    {
        await using var db = _fx.Db();
        var page = await new EmReportService(db).GetTasksAsync(new EmReportTaskRegisterQueryDto { Search = created.Title, PageSize = 50 }, default);
        return Assert.Single(page.Items);
    }

    [Fact]
    public async Task EmReport_TatEnabledDelegation_IsMeasuredByTat_NotByItsEndDate()
    {
        // End date long past would be "Delayed" by the old rule; 120 active minutes within a 240-minute TAT is OnTime.
        var type = Unique("EmOnTime");
        await AddRuleAsync(type, null, 240);
        var created = await CreateAsync(type, endDate: IndiaBusinessCalendar.Today.AddDays(-30));
        await Run(s => s.StartAsync(created.DelegationId));
        await BackdateStartAsync(created.DelegationId, created.EaTaskId, 120);
        await Run(s => s.CompleteAsync(created.DelegationId, null));

        var row = await RegisterRow(created);

        Assert.Equal("OnTime", row.Performance);
        Assert.Equal(240, row.AllottedTatMinutes);        // existing task-register TAT fields, no new ones
        Assert.Equal(120, row.CurrentOrFinalTatUsedMinutes);
        Assert.Equal(created.EndDate, row.DueDate);       // the end date is still shown, just not evaluated
    }

    [Fact]
    public async Task EmReport_TatEnabledDelegation_OverItsTat_IsDelayed_EvenWithAFutureEndDate()
    {
        var type = Unique("EmDelayed");
        await AddRuleAsync(type, null, 60);
        var created = await CreateAsync(type, endDate: IndiaBusinessCalendar.Today.AddDays(30));
        await Run(s => s.StartAsync(created.DelegationId));
        await BackdateStartAsync(created.DelegationId, created.EaTaskId, 120);
        await Run(s => s.CompleteAsync(created.DelegationId, null));

        Assert.Equal("Delayed", (await RegisterRow(created)).Performance);
    }

    [Fact]
    public async Task EmReport_ActiveTatDelegation_UsesTheLiveTatPosition_AndAPausedOneIsPaused()
    {
        var type = Unique("EmActive");
        await AddRuleAsync(type, null, 60);
        var created = await CreateAsync(type);
        await Run(s => s.StartAsync(created.DelegationId));
        await BackdateStartAsync(created.DelegationId, created.EaTaskId, 90);   // 90 of 60 minutes used, still running

        var running = await RegisterRow(created);
        Assert.Equal("Delayed", running.Performance);
        Assert.False(running.IsPaused);

        await Run(s => s.PauseAsync(created.DelegationId, null));
        await SetPauseWindowAsync(created.EaTaskId, 30, null);   // frozen at 30 of 60
        var paused = await RegisterRow(created);
        Assert.True(paused.IsPaused);
        Assert.Equal(EaTaskExecutionStatus.InProgress, paused.ExecutionStatus);
        Assert.Equal("OnTime", paused.Performance);
        Assert.Equal(30, paused.CurrentOrFinalTatUsedMinutes);
    }

    [Fact]
    public async Task EmReport_DelegationWithoutATatSnapshot_KeepsTheEndDateRule()
    {
        var late = await CreateAsync(null, endDate: IndiaBusinessCalendar.Today.AddDays(-3));
        await Run(s => s.StartAsync(late.DelegationId));
        await Run(s => s.CompleteAsync(late.DelegationId, null));
        var early = await CreateAsync(null, endDate: IndiaBusinessCalendar.Today.AddDays(30));
        await Run(s => s.StartAsync(early.DelegationId));
        await Run(s => s.CompleteAsync(early.DelegationId, null));
        var noDate = await CreateAsync(null);

        Assert.Equal("Delayed", (await RegisterRow(late)).Performance);
        Assert.Equal("OnTime", (await RegisterRow(early)).Performance);
        Assert.Equal("NotMeasured", (await RegisterRow(noDate)).Performance);
        var row = await RegisterRow(late);
        Assert.Null(row.AllottedTatMinutes);
        Assert.Null(row.CurrentOrFinalTatUsedMinutes);
    }

    // ============================================================
    // CONTRACT — one classification, nothing hardcoded
    // ============================================================
    [Fact]
    public void Delegation_HasNoSubtypeAnywhere_AndNoHardcodedTypeOptions()
    {
        foreach (var t in new[] { typeof(DelegationCreateRequestDto), typeof(DelegationUpdateRequestDto), typeof(DelegationResponseDto),
                     typeof(DelegationListQueryDto), typeof(DelegationCreateCommand), typeof(DelegationEntity) })
            Assert.DoesNotContain(t.GetProperties(), p => p.Name.Contains("Subtype", StringComparison.OrdinalIgnoreCase));

        var assembly = typeof(DelegationEntity).Assembly;
        Assert.DoesNotContain(assembly.GetTypes(), t => t.IsEnum && t.Name.Contains("DelegationType", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(EaFmsDbContext).GetProperties(), p => p.Name.Contains("DelegationType", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(DelegationService).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string) && f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!),
            v => v.Contains("Self Delegation") || v.Contains("Director Delegation") || v == "General");
    }
}
