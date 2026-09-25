using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.Ai;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Delegation;

/// <summary>
/// Delegation AI (owner suggestion, due-date prediction, delay-risk check). All three read
/// via the REAL IDelegationService.GetByIdAsync (not mocked) so these tests prove genuine
/// reuse of its already-assembled DelegationResponseDto — no parallel due-state/TAT logic.
/// IClaudeClient is mocked — no real Anthropic API call is ever made from this test suite.
/// The delegation under test is created via the REAL DelegationService (same pattern as
/// DelegationServiceTests); historical background rows are inserted directly since they
/// only need correct field values, not full lifecycle fidelity.
/// </summary>
public class DelegationAiServiceTests
{
    private const string ModuleName = DelegationService.DelegationBusinessModuleName;

    private static EaFmsDbContext NewDb() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);

    private sealed class Harness
    {
        public required EaFmsDbContext Db { get; init; }
        public required DelegationService Delegations { get; init; }
        public required long ModuleId { get; init; }
    }

    private static async Task<Harness> NewHarnessAsync()
    {
        var db = NewDb();
        db.BusinessModules.Add(new BusinessModule { Name = ModuleName, IsActive = true, IsDeleted = false, CreatedBy = "tester", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var moduleId = db.BusinessModules.Single().Id;

        var numbers = new Mock<IDelegationNumberRepository>();
        var seq = 0;
        numbers.Setup(r => r.GenerateNextReferenceNoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"DLG-AI-{Interlocked.Increment(ref seq):D4}");

        Func<CreateEaTaskDto, CancellationToken, Task<EaTaskResponseDto>> insertTask = (dto, _) =>
        {
            var task = new EaTask
            {
                BusinessModuleId = moduleId, ModuleName = ModuleName, BusinessRecordId = dto.BusinessRecordId,
                Task = dto.Task, Description = dto.Description, AllottedTatMinutes = null,
                ExecutionStatus = "NotStarted", IsActive = true, CreatedBy = "tester", CreatedDate = DateTime.UtcNow,
            };
            db.Tasks.Add(task);
            db.SaveChanges();
            return Task.FromResult(new EaTaskResponseDto
            {
                EaTaskId = task.Id, ModuleId = moduleId, ModuleName = ModuleName,
                BusinessRecordId = task.BusinessRecordId, Task = task.Task, Description = task.Description,
                ExecutionStatus = task.ExecutionStatus, IsActive = true, CreatedBy = task.CreatedBy, CreatedDate = task.CreatedDate,
            });
        };

        var eaTasks = new Mock<IEaTaskService>();
        // CreateAsync uses CreateWithoutTatAsync when DelegationType is null and
        // CreateWithTypeOnlyTatAsync when it is set (DelegationService.CreateCoreAsync) — both
        // must be stubbed identically, since the real EaTaskService runs Postgres-only raw SQL
        // and cannot execute against the InMemory provider used by this test class.
        eaTasks.Setup(s => s.CreateWithoutTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()))
            .Returns(insertTask);
        eaTasks.Setup(s => s.CreateWithTypeOnlyTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()))
            .Returns(insertTask);

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "manager-1" && u.UserId == 42L);
        var audit = new AuditService(db, user);
        var delegations = new DelegationService(db, user, audit, numbers.Object, eaTasks.Object,
            Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(),
            new TaskReviewService(db, new TaskReviewRepository(db), user, audit),
            new TatRuleRepository(db));

        return new Harness { Db = db, Delegations = delegations, ModuleId = moduleId };
    }

    private static (Mock<IClaudeClient> Claude, Mock<IDelegationAiPromptBuilder> Prompts) Mocks()
    {
        var claude = new Mock<IClaudeClient>();
        var prompts = new Mock<IDelegationAiPromptBuilder>();
        prompts.Setup(p => p.BuildOwnerSystemPrompt()).Returns("system");
        prompts.Setup(p => p.BuildOwnerUserPrompt(It.IsAny<DelegationResponseDto>(), It.IsAny<List<(string, string?, int)>>())).Returns("user");
        prompts.Setup(p => p.BuildDueDateSystemPrompt()).Returns("system");
        prompts.Setup(p => p.BuildDueDateUserPrompt(It.IsAny<DelegationResponseDto>(), It.IsAny<string>(), It.IsAny<DateTime>())).Returns("user");
        prompts.Setup(p => p.BuildDelayRiskSystemPrompt()).Returns("system");
        prompts.Setup(p => p.BuildDelayRiskUserPrompt(It.IsAny<DelegationResponseDto>())).Returns("user");
        return (claude, prompts);
    }

    private static IServiceProvider MakeServiceProvider(IClaudeClient claude)
    {
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(IClaudeClient))).Returns(claude);
        return sp.Object;
    }

    private static DelegationAiService Service(
        Harness h, Mock<IClaudeClient> claude, Mock<IDelegationAiPromptBuilder> prompts, int maxRetries = 2) =>
        new(h.Db, MakeServiceProvider(claude.Object), prompts.Object, h.Delegations, new TatRuleRepository(h.Db), new DelegationAiRepository(h.Db),
            NullLogger<DelegationAiService>.Instance, Options.Create(new ClaudeOptions { MaxRetries = maxRetries }));

    private static DelegationCreateRequestDto MakeCreateDto(string? delegationType = "Report", string doerId = "emp-1", string doerName = "Doer One") => new()
    {
        Title = "Prepare board deck",
        DelegationType = delegationType,
        DoerId = doerId,
        DoerNameSnapshot = doerName,
        StartDate = DateTime.UtcNow.Date,
        EndDate = DateTime.UtcNow.Date.AddDays(3),
    };

    // Inserts a historical Completed delegation directly (bypassing the full lifecycle —
    // only the field values matter for these queries, not lifecycle fidelity).
    private static async Task<Jarvis5.Entities.EaFms.Delegation> SeedCompletedHistoryAsync(
        EaFmsDbContext db, long moduleId, string delegationType, string doerId, string? doerName, DateTime startedAt, DateTime completedAt)
    {
        var task = new EaTask
        {
            BusinessModuleId = moduleId, ModuleName = ModuleName, BusinessRecordId = Guid.NewGuid().ToString("N"),
            Task = "history", ExecutionStatus = "Completed", IsActive = true, CreatedBy = "tester", CreatedDate = startedAt,
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var entity = new Jarvis5.Entities.EaFms.Delegation
        {
            ReferenceNo = $"DLG-HIST-{Guid.NewGuid():N}", EaTaskId = task.Id, Title = "Past work",
            DoerId = doerId, DoerNameSnapshot = doerName, AssignedById = "manager-1",
            DelegationType = delegationType, Status = "Completed",
            StartedAt = startedAt, CompletedAt = completedAt,
            CreatedBy = "tester", CreatedDate = startedAt,
        };
        db.Delegations.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    // ============================================================
    // Suggest owner — no directory exists, so this must never invent an identity
    // ============================================================

    [Fact]
    public async Task SuggestOwner_NoDelegationType_ReturnsNullWithoutCallingClaude()
    {
        var h = await NewHarnessAsync();
        var created = await h.Delegations.CreateAsync(MakeCreateDto(delegationType: null));
        var (claude, prompts) = Mocks();

        var result = await Service(h, claude, prompts).SuggestOwnerAsync(created.DelegationId);

        Assert.Null(result.SuggestedDoerId);
        Assert.Equal(0, result.HistoricalSampleSize);
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SuggestOwner_NoHistoryForType_ReturnsNullWithoutCallingClaude()
    {
        var h = await NewHarnessAsync();
        var created = await h.Delegations.CreateAsync(MakeCreateDto(delegationType: "NeverSeenBefore"));
        var (claude, prompts) = Mocks();

        var result = await Service(h, claude, prompts).SuggestOwnerAsync(created.DelegationId);

        Assert.Null(result.SuggestedDoerId);
        Assert.Contains("No past delegations", result.Reasoning);
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SuggestOwner_WithHistory_HonorsAiPick_WhenItIsARealCandidate()
    {
        var h = await NewHarnessAsync();
        var now = DateTime.UtcNow;
        await SeedCompletedHistoryAsync(h.Db, h.ModuleId, "Report", "emp-1", "Priya", now.AddDays(-10), now.AddDays(-9));
        await SeedCompletedHistoryAsync(h.Db, h.ModuleId, "Report", "emp-1", "Priya", now.AddDays(-8), now.AddDays(-7));
        await SeedCompletedHistoryAsync(h.Db, h.ModuleId, "Report", "emp-2", "Ramesh", now.AddDays(-6), now.AddDays(-5));
        var current = await h.Delegations.CreateAsync(MakeCreateDto(delegationType: "Report"));
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"recommendedDoerId":"emp-2","reasoning":"Ramesh has handled similar smaller reports."}""");

        var result = await Service(h, claude, prompts).SuggestOwnerAsync(current.DelegationId);

        Assert.Equal("emp-2", result.SuggestedDoerId); // AI's real, valid pick honored — not forced to top-by-count
        Assert.Equal("Ramesh", result.SuggestedDoerName);
        Assert.Equal(3, result.HistoricalSampleSize);
    }

    [Fact]
    public async Task SuggestOwner_AiReturnsIdNotInCandidateList_FallsBackToTopCandidate()
    {
        var h = await NewHarnessAsync();
        var now = DateTime.UtcNow;
        await SeedCompletedHistoryAsync(h.Db, h.ModuleId, "Report", "emp-1", "Priya", now.AddDays(-10), now.AddDays(-9));
        await SeedCompletedHistoryAsync(h.Db, h.ModuleId, "Report", "emp-1", "Priya", now.AddDays(-8), now.AddDays(-7));
        await SeedCompletedHistoryAsync(h.Db, h.ModuleId, "Report", "emp-2", "Ramesh", now.AddDays(-6), now.AddDays(-5));
        var current = await h.Delegations.CreateAsync(MakeCreateDto(delegationType: "Report"));
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"recommendedDoerId":"emp-never-seen","reasoning":"Best fit."}""");

        var result = await Service(h, claude, prompts).SuggestOwnerAsync(current.DelegationId);

        Assert.Equal("emp-1", result.SuggestedDoerId); // falls back to the most-frequent REAL candidate
    }

    [Fact]
    public async Task SuggestOwner_UnknownDelegation_ThrowsNotFound()
    {
        var h = await NewHarnessAsync();
        var (claude, prompts) = Mocks();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(h, claude, prompts).SuggestOwnerAsync(999999));
    }

    // ============================================================
    // Apply suggested owner — writes the EA's reviewed/edited choice for real, without
    // clobbering the delegation's other editable fields (read-modify-write over a
    // full-replace Update endpoint)
    // ============================================================

    [Fact]
    public async Task ApplySuggestedOwner_WritesDoerOntoTheRealDelegation_WithoutClobberingOtherFields()
    {
        var h = await NewHarnessAsync();
        var created = await h.Delegations.CreateAsync(MakeCreateDto(delegationType: null, doerId: "emp-1", doerName: "Old Doer"));
        var (claude, prompts) = Mocks();

        var result = await Service(h, claude, prompts).ApplySuggestedOwnerAsync(
            created.DelegationId, new ApplySuggestedOwnerRequestDto { DoerId = "emp-2", DoerName = "New Doer" });

        Assert.Equal("emp-2", result.DoerId);
        Assert.Equal("New Doer", result.DoerName);
        Assert.Equal("Prepare board deck", result.Title); // untouched
        Assert.Equal(created.EndDate, result.EndDate); // untouched
    }

    // Applying links back to whichever suggestion preceded it, marking that row IsApplied.
    [Fact]
    public async Task ApplySuggestedOwner_AfterSuggest_MarksTheSuggestionApplied()
    {
        var h = await NewHarnessAsync();
        var now = DateTime.UtcNow;
        await SeedCompletedHistoryAsync(h.Db, h.ModuleId, "Report", "emp-1", "Priya", now.AddDays(-10), now.AddDays(-9));
        var created = await h.Delegations.CreateAsync(MakeCreateDto(delegationType: "Report"));
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"recommendedDoerId":"emp-1","reasoning":"Only candidate."}""");
        await Service(h, claude, prompts).SuggestOwnerAsync(created.DelegationId);

        await Service(h, claude, prompts).ApplySuggestedOwnerAsync(
            created.DelegationId, new ApplySuggestedOwnerRequestDto { DoerId = "emp-1", DoerName = "Priya" });

        var suggestion = await h.Db.DelegationOwnerSuggestions.SingleAsync();
        Assert.True(suggestion.IsApplied);
        Assert.NotNull(suggestion.AppliedAt);
        Assert.Equal("emp-1", suggestion.AppliedDoerId);
        Assert.Equal("Priya", suggestion.AppliedDoerName);
    }

    [Fact]
    public async Task ApplySuggestedOwner_OnCompletedDelegation_ThrowsBusinessRuleException()
    {
        var h = await NewHarnessAsync();
        var created = await h.Delegations.CreateAsync(MakeCreateDto());
        var entity = await h.Db.Delegations.SingleAsync(d => d.Id == created.DelegationId);
        entity.Status = "Completed";
        await h.Db.SaveChangesAsync();
        var (claude, prompts) = Mocks();

        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(h, claude, prompts).ApplySuggestedOwnerAsync(
            created.DelegationId, new ApplySuggestedOwnerRequestDto { DoerId = "emp-2" }));
    }

    [Fact]
    public async Task ApplySuggestedOwner_UnknownDelegation_ThrowsNotFound()
    {
        var h = await NewHarnessAsync();
        var (claude, prompts) = Mocks();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(h, claude, prompts).ApplySuggestedOwnerAsync(
            999999, new ApplySuggestedOwnerRequestDto { DoerId = "emp-2" }));
    }

    // ============================================================
    // Predict due date — the date is always computed in code, never by Claude
    // ============================================================

    [Fact]
    public async Task PredictDueDate_CompletedDelegation_ThrowsBusinessRuleException()
    {
        var h = await NewHarnessAsync();
        var created = await h.Delegations.CreateAsync(MakeCreateDto());
        var entity = await h.Db.Delegations.SingleAsync(d => d.Id == created.DelegationId);
        entity.Status = "Completed";
        await h.Db.SaveChangesAsync();
        var (claude, prompts) = Mocks();

        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(h, claude, prompts).PredictDueDateAsync(created.DelegationId));
    }

    [Fact]
    public async Task PredictDueDate_WithConfiguredTatRule_UsesRealTatMinutesNotClaudeMath()
    {
        var h = await NewHarnessAsync();
        h.Db.TatRules.Add(new TatRule
        {
            BusinessModuleId = h.ModuleId, ModuleName = ModuleName, Type = "Report", TaskType = "Actual",
            TatMinutes = 120, IsActive = true, CreatedBy = "tester", CreatedDate = DateTime.UtcNow,
        });
        await h.Db.SaveChangesAsync();
        var created = await h.Delegations.CreateAsync(MakeCreateDto(delegationType: "Report"));
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"explanation":"Based on the configured 2-hour turnaround for this type."}""");

        var result = await Service(h, claude, prompts).PredictDueDateAsync(created.DelegationId);

        Assert.Equal("ConfiguredTat", result.Basis);
        var expectedAnchor = created.StartDate!.Value; // not started yet, falls back to planned StartDate
        Assert.Equal(expectedAnchor.AddMinutes(120), result.SuggestedDueDate);
        Assert.Equal("Based on the configured 2-hour turnaround for this type.", result.Explanation);
    }

    [Fact]
    public async Task PredictDueDate_NoTatRule_UsesRealHistoricalAverage()
    {
        var h = await NewHarnessAsync();
        var now = DateTime.UtcNow;
        // Two past completions of the same type: 60 minutes and 180 minutes -> average 120 minutes.
        await SeedCompletedHistoryAsync(h.Db, h.ModuleId, "Report", "emp-1", "Priya", now.AddDays(-10), now.AddDays(-10).AddMinutes(60));
        await SeedCompletedHistoryAsync(h.Db, h.ModuleId, "Report", "emp-1", "Priya", now.AddDays(-8), now.AddDays(-8).AddMinutes(180));
        var created = await h.Delegations.CreateAsync(MakeCreateDto(delegationType: "Report"));
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"explanation":"Based on the average of two similar past delegations."}""");

        var result = await Service(h, claude, prompts).PredictDueDateAsync(created.DelegationId);

        Assert.Equal("HistoricalAverage", result.Basis);
        var expectedAnchor = created.StartDate!.Value;
        Assert.Equal(expectedAnchor.AddMinutes(120), result.SuggestedDueDate);
    }

    [Fact]
    public async Task PredictDueDate_NoTatRuleNoHistory_ReturnsNoneBasisWithoutCallingClaude()
    {
        var h = await NewHarnessAsync();
        var created = await h.Delegations.CreateAsync(MakeCreateDto(delegationType: "NeverSeenType"));
        var (claude, prompts) = Mocks();

        var result = await Service(h, claude, prompts).PredictDueDateAsync(created.DelegationId);

        Assert.Equal("None", result.Basis);
        Assert.Null(result.SuggestedDueDate);
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PredictDueDate_UnknownDelegation_ThrowsNotFound()
    {
        var h = await NewHarnessAsync();
        var (claude, prompts) = Mocks();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(h, claude, prompts).PredictDueDateAsync(999999));
    }

    // ============================================================
    // Apply predicted due date — writes the EA's reviewed/edited date for real, without
    // clobbering the delegation's other editable fields
    // ============================================================

    [Fact]
    public async Task ApplyPredictedDueDate_WritesEndDateOntoTheRealDelegation_WithoutClobberingOtherFields()
    {
        var h = await NewHarnessAsync();
        var created = await h.Delegations.CreateAsync(MakeCreateDto(delegationType: null));
        var (claude, prompts) = Mocks();
        var newEndDate = created.EndDate!.Value.AddDays(5);

        var result = await Service(h, claude, prompts).ApplyPredictedDueDateAsync(
            created.DelegationId, new ApplyPredictedDueDateRequestDto { EndDate = newEndDate });

        Assert.Equal(newEndDate, result.EndDate);
        Assert.Equal("Prepare board deck", result.Title); // untouched
        Assert.Equal(created.DoerId, result.DoerId); // untouched
    }

    // Applying links back to whichever prediction preceded it, marking that row IsApplied.
    [Fact]
    public async Task ApplyPredictedDueDate_AfterPredict_MarksTheSuggestionApplied()
    {
        var h = await NewHarnessAsync();
        var created = await h.Delegations.CreateAsync(MakeCreateDto(delegationType: "NeverSeenType"));
        var (claude, prompts) = Mocks();
        await Service(h, claude, prompts).PredictDueDateAsync(created.DelegationId); // logs a "None" basis row
        var newEndDate = created.EndDate!.Value.AddDays(5);

        await Service(h, claude, prompts).ApplyPredictedDueDateAsync(
            created.DelegationId, new ApplyPredictedDueDateRequestDto { EndDate = newEndDate });

        var suggestion = await h.Db.DelegationDueDatePredictions.SingleAsync();
        Assert.True(suggestion.IsApplied);
        Assert.NotNull(suggestion.AppliedAt);
        Assert.Equal(newEndDate, suggestion.AppliedDueDate);
    }

    [Fact]
    public async Task ApplyPredictedDueDate_OnCompletedDelegation_ThrowsBusinessRuleException()
    {
        var h = await NewHarnessAsync();
        var created = await h.Delegations.CreateAsync(MakeCreateDto());
        var entity = await h.Db.Delegations.SingleAsync(d => d.Id == created.DelegationId);
        entity.Status = "Completed";
        await h.Db.SaveChangesAsync();
        var (claude, prompts) = Mocks();

        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(h, claude, prompts).ApplyPredictedDueDateAsync(
            created.DelegationId, new ApplyPredictedDueDateRequestDto { EndDate = DateTime.UtcNow.AddDays(3) }));
    }

    [Fact]
    public async Task ApplyPredictedDueDate_UnknownDelegation_ThrowsNotFound()
    {
        var h = await NewHarnessAsync();
        var (claude, prompts) = Mocks();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(h, claude, prompts).ApplyPredictedDueDateAsync(
            999999, new ApplyPredictedDueDateRequestDto { EndDate = DateTime.UtcNow.AddDays(3) }));
    }

    // ============================================================
    // Delay risk check
    // ============================================================

    [Fact]
    public async Task CheckDelayRisk_CompletedDelegation_ThrowsBusinessRuleException()
    {
        var h = await NewHarnessAsync();
        var created = await h.Delegations.CreateAsync(MakeCreateDto());
        var entity = await h.Db.Delegations.SingleAsync(d => d.Id == created.DelegationId);
        entity.Status = "Completed";
        await h.Db.SaveChangesAsync();
        var (claude, prompts) = Mocks();

        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(h, claude, prompts).CheckDelayRiskAsync(created.DelegationId));
    }

    [Fact]
    public async Task CheckDelayRisk_ReturnsAiAssessment_NormalizesRiskLevel()
    {
        var h = await NewHarnessAsync();
        var created = await h.Delegations.CreateAsync(MakeCreateDto());
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"riskLevel":"high","reasoning":"Not started yet and due soon.","suggestedNudgeMessage":"Hi, just checking in on this."}""");

        var result = await Service(h, claude, prompts).CheckDelayRiskAsync(created.DelegationId);

        Assert.Equal("High", result.RiskLevel); // normalized casing
        Assert.Equal("Not started yet and due soon.", result.Reasoning);
        Assert.Equal("Hi, just checking in on this.", result.SuggestedNudgeMessage);
    }

    // Full audit trail: every generated suggestion is logged verbatim into its own
    // task-specific table, mirroring SCIH's separate SCIH_Analysis/SCIH_SolutionDesign
    // tables — see DelegationDelayRiskCheck's own doc comment.
    [Fact]
    public async Task CheckDelayRisk_WritesDelegationDelayRiskCheckRow()
    {
        var h = await NewHarnessAsync();
        var created = await h.Delegations.CreateAsync(MakeCreateDto());
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"riskLevel":"Low","reasoning":"Plenty of time.","suggestedNudgeMessage":null}""");

        await Service(h, claude, prompts).CheckDelayRiskAsync(created.DelegationId);

        var log = Assert.Single(h.Db.DelegationDelayRiskChecks);
        Assert.Equal(created.DelegationId, log.DelegationId);
        Assert.Equal("Low", log.RiskLevel);
        Assert.Contains("Plenty of time", log.Reasoning);
    }

    [Fact]
    public async Task CheckDelayRisk_AiReturnsInvalidRiskLevel_FallsBackToMedium()
    {
        var h = await NewHarnessAsync();
        var created = await h.Delegations.CreateAsync(MakeCreateDto());
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"riskLevel":"Severe","reasoning":"x","suggestedNudgeMessage":null}""");

        var result = await Service(h, claude, prompts).CheckDelayRiskAsync(created.DelegationId);

        Assert.Equal("Medium", result.RiskLevel);
        Assert.Null(result.SuggestedNudgeMessage);
    }

    [Fact]
    public async Task CheckDelayRisk_UnknownDelegation_ThrowsNotFound()
    {
        var h = await NewHarnessAsync();
        var (claude, prompts) = Mocks();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(h, claude, prompts).CheckDelayRiskAsync(999999));
    }

    [Fact]
    public async Task CheckDelayRisk_MalformedJson_RetriesThenSucceeds()
    {
        var h = await NewHarnessAsync();
        var created = await h.Delegations.CreateAsync(MakeCreateDto());
        var (claude, prompts) = Mocks();
        claude.SetupSequence(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("not json")
            .ReturnsAsync("""{"riskLevel":"Low","reasoning":"Plenty of time.","suggestedNudgeMessage":null}""");

        var result = await Service(h, claude, prompts, maxRetries: 2).CheckDelayRiskAsync(created.DelegationId);

        Assert.Equal("Low", result.RiskLevel);
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ============================================================
    // EA AI usage log: every call through the real service is stored, even the invalid-JSON attempt
    // ============================================================

    [Fact]
    public async Task CheckDelayRisk_EveryAiCall_IsStoredInTheEaAiUsageLog_WithTaskAndTokens()
    {
        var h = await NewHarnessAsync();
        var created = await h.Delegations.CreateAsync(MakeCreateDto());
        var (claude, prompts) = Mocks();
        var replies = new Queue<string>(new[] { "not json at all", """{"riskLevel":"low","reasoning":"Fine."}""" });
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var usage = ClaudeUsageCapture.Current!;   // what the real ClaudeClient fills while streaming
                usage.InputTokens = 700; usage.OutputTokens = 40; usage.Model = "claude-test";
                return replies.Dequeue();
            });
        var options = (DbContextOptions<EaFmsDbContext>)Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions
            .GetService<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptions>(h.Db);
        var ctx = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.Path = $"/api/ea/delegations/{created.DelegationId}/ai/delay-risk";
        ctx.Request.RouteValues["delegationId"] = created.DelegationId.ToString();
        ctx.Request.Headers["X-Employee-Name"] = "Siddhi Jadhav";
        var aiUsage = new EaAiUsageLogger(options, Jarvis5.Tests.EaFms.Followups.FollowupTestSupport.User(null, null),
            new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = ctx }, NullLogger<EaAiUsageLogger>.Instance);
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(IClaudeClient))).Returns(claude.Object);
        sp.Setup(s => s.GetService(typeof(IEaAiUsageLogger))).Returns(aiUsage);
        var service = new DelegationAiService(h.Db, sp.Object, prompts.Object, h.Delegations, new TatRuleRepository(h.Db), new DelegationAiRepository(h.Db),
            NullLogger<DelegationAiService>.Instance, Options.Create(new ClaudeOptions { MaxRetries = 2 }));

        var result = await service.CheckDelayRiskAsync(created.DelegationId);

        Assert.Equal("Low", result.RiskLevel);
        await using var check = new EaFmsDbContext(options);
        var rows = await check.AiUsageLogs.AsNoTracking().OrderBy(r => r.Id).ToListAsync();
        Assert.Equal(new[] { ("InvalidJson", 1), ("Succeeded", 2) }, rows.Select(r => (r.Status, r.AttemptNo)));
        Assert.All(rows, r =>
        {
            Assert.Equal(("Delegation", created.DelegationId.ToString(), (long?)created.EaTaskId, "Siddhi Jadhav"),
                (r.Module, r.BusinessRecordId, r.EaTaskId, r.RequestedByName));
            Assert.Equal((long?)740, r.TotalTokens);
            Assert.Contains("delay risk", r.Feature, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Equal("not json at all", rows[0].ResponseText);
    }

    [Fact]
    public async Task ApplyPredictedDueDate_MarksTheStoredAiResponse_AsUsed_WithTheDateTheEaChose()
    {
        var h = await NewHarnessAsync();
        var created = await h.Delegations.CreateAsync(MakeCreateDto());
        var (claude, prompts) = Mocks();
        var options = (DbContextOptions<EaFmsDbContext>)Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions
            .GetService<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptions>(h.Db);
        await using (var seed = new EaFmsDbContext(options))
        {
            seed.AiUsageLogs.Add(new EaAiUsageLog { Module = "Delegation", Feature = "Delegation due date explanation",
                BusinessRecordId = created.DelegationId.ToString(), Status = "Succeeded", AttemptNo = 1, ResponseText = "{}",
                RequestedAt = DateTime.UtcNow.AddMinutes(-1), CompletedAt = DateTime.UtcNow.AddMinutes(-1), CreatedDate = DateTime.UtcNow });
            await seed.SaveChangesAsync();
        }
        var ctx = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        ctx.Request.Headers["X-Employee-Name"] = "Siddhi Jadhav";
        var aiUsage = new EaAiUsageLogger(options, Jarvis5.Tests.EaFms.Followups.FollowupTestSupport.User(null, null),
            new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = ctx }, NullLogger<EaAiUsageLogger>.Instance);
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(IClaudeClient))).Returns(claude.Object);
        sp.Setup(s => s.GetService(typeof(IEaAiUsageLogger))).Returns(aiUsage);
        var service = new DelegationAiService(h.Db, sp.Object, prompts.Object, h.Delegations, new TatRuleRepository(h.Db), new DelegationAiRepository(h.Db),
            NullLogger<DelegationAiService>.Instance, Options.Create(new ClaudeOptions { MaxRetries = 2 }));
        var chosen = DateTime.UtcNow.Date.AddDays(9);

        await service.ApplyPredictedDueDateAsync(created.DelegationId, new ApplyPredictedDueDateRequestDto { EndDate = chosen });

        await using var check = new EaFmsDbContext(options);
        var row = await check.AiUsageLogs.AsNoTracking().SingleAsync();
        Assert.True(row.IsUsed);
        Assert.Equal("Siddhi Jadhav", row.UsedByName);
        Assert.Contains(chosen.ToString("yyyy-MM-dd"), row.UsedValue);
    }
}
