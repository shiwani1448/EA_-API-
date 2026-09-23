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

namespace Jarvis5.Tests.EaFms.Approvals;

/// <summary>
/// Approval AI (readiness check, approver suggestion, status summary). All three are
/// preview-only and read via the REAL ApprovalQueryService (not mocked) so these tests
/// prove genuine reuse of its already-assembled ApprovalDetailDto — no parallel
/// due-state/history logic. IClaudeClient is mocked — no real Anthropic API call is ever
/// made from this test suite. Requests are seeded via the REAL ApprovalService/
/// ApprovalLifecycleService (same as ApprovalCreateSubmitTests) rather than hand-built
/// entities, so seeded data always satisfies ApprovalQueryService's own module/task joins.
/// </summary>
public class ApprovalAiServiceTests
{
    private static EaFmsDbContext NewDb() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);

    private sealed class Harness
    {
        public required EaFmsDbContext Db { get; init; }
        public required Jarvis5.Services.EaFms.ApprovalService Approvals { get; init; }
        public required ApprovalLifecycleService Lifecycle { get; init; }
        public required ApprovalQueryService Queries { get; init; }
    }

    private static async Task<Harness> NewHarnessAsync()
    {
        var db = NewDb();
        db.BusinessModules.Add(new BusinessModule
        {
            Name = "EA Approval", IsActive = true, IsDeleted = false,
            CreatedBy = "tester", CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var moduleId = db.BusinessModules.Single().Id;

        var numbers = new Mock<IApprovalNumberRepository>();
        var seq = 0;
        numbers.Setup(r => r.GenerateNextReferenceNoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"APR-AI-{Interlocked.Increment(ref seq):D4}");

        var eaTasks = new Mock<IEaTaskService>();
        eaTasks.Setup(s => s.CreateWithoutTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateEaTaskDto dto, CancellationToken _) =>
            {
                var task = new EaTask
                {
                    BusinessModuleId = moduleId, ModuleName = "EA Approval",
                    BusinessRecordId = dto.BusinessRecordId, Task = dto.Task, Description = dto.Description,
                    ExecutionStatus = "NotStarted", IsActive = true,
                    CreatedBy = "tester", CreatedDate = DateTime.UtcNow,
                };
                db.Tasks.Add(task);
                db.SaveChanges();
                return new EaTaskResponseDto
                {
                    EaTaskId = task.Id, ModuleId = moduleId, ModuleName = "EA Approval",
                    BusinessRecordId = task.BusinessRecordId, Task = task.Task, Description = task.Description,
                    ExecutionStatus = task.ExecutionStatus, IsActive = true,
                    CreatedBy = task.CreatedBy, CreatedDate = task.CreatedDate,
                };
            });

        var audit = Mock.Of<IAuditService>();
        var approvals = new Jarvis5.Services.EaFms.ApprovalService(db, audit, numbers.Object, eaTasks.Object);
        var taskReview = new TaskReviewService(db, new TaskReviewRepository(db), Mock.Of<ICurrentUserService>(), audit);
        var lifecycle = new ApprovalLifecycleService(db, audit, taskReview);
        var documents = new ApprovalDocumentService(db, Mock.Of<ICurrentUserService>(), audit,
            Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(e => e.ContentRootPath == Path.GetTempPath()),
            new ApprovalAuthorizationService(db), lifecycle);
        var queries = new ApprovalQueryService(db, documents, taskReview);

        return new Harness { Db = db, Approvals = approvals, Lifecycle = lifecycle, Queries = queries };
    }

    private static (Mock<IClaudeClient> Claude, Mock<IApprovalAiPromptBuilder> Prompts) Mocks()
    {
        var claude = new Mock<IClaudeClient>();
        var prompts = new Mock<IApprovalAiPromptBuilder>();
        prompts.Setup(p => p.BuildReadinessSystemPrompt()).Returns("system");
        prompts.Setup(p => p.BuildReadinessUserPrompt(It.IsAny<ApprovalDetailDto>())).Returns("user");
        prompts.Setup(p => p.BuildApproverSystemPrompt()).Returns("system");
        prompts.Setup(p => p.BuildApproverUserPrompt(It.IsAny<ApprovalDetailDto>(), It.IsAny<List<(string, int)>>())).Returns("user");
        prompts.Setup(p => p.BuildStatusSystemPrompt()).Returns("system");
        prompts.Setup(p => p.BuildStatusUserPrompt(It.IsAny<ApprovalDetailDto>())).Returns("user");
        return (claude, prompts);
    }

    private static IServiceProvider MakeServiceProvider(IClaudeClient claude)
    {
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(IClaudeClient))).Returns(claude);
        return sp.Object;
    }

    private static ApprovalAiService Service(
        Harness h, Mock<IClaudeClient> claude, Mock<IApprovalAiPromptBuilder> prompts, int maxRetries = 2) =>
        new(h.Db, MakeServiceProvider(claude.Object), prompts.Object, h.Queries,
            NullLogger<ApprovalAiService>.Instance, Options.Create(new ClaudeOptions { MaxRetries = maxRetries }));

    private const string ValidReadinessJson = """{"isLikelyReady":false,"missingFields":["Justification"],"suggestedDocuments":["Invoice or quote"],"notes":"No supporting documents attached."}""";
    private const string ValidStatusJson = """{"summary":"Pending initial approval, no changes requested yet."}""";

    // ============================================================
    // Readiness check
    // ============================================================

    [Fact]
    public async Task CheckReadiness_ReturnsAiJudgement_AndAlwaysWarnsAboutFileContents()
    {
        var h = await NewHarnessAsync();
        var created = await h.Approvals.CreateAsync(new ApprovalRequest { RequestTitle = "Capex", RequestType = "Finance", Department = "Ops", CreatedBy = "creator" });
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ValidReadinessJson);

        var result = await Service(h, claude, prompts).CheckReadinessAsync(created.Id);

        Assert.False(result.IsLikelyReady);
        Assert.Contains("Justification", result.MissingFields);
        Assert.Contains("Invoice or quote", result.SuggestedDocuments);
        Assert.NotNull(result.WarningMessage);
        Assert.Contains("cannot read the contents", result.WarningMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckReadiness_UnknownApproval_ThrowsNotFound()
    {
        var h = await NewHarnessAsync();
        var (claude, prompts) = Mocks();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(h, claude, prompts).CheckReadinessAsync(999999));
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckReadiness_MalformedJson_RetriesThenSucceeds()
    {
        var h = await NewHarnessAsync();
        var created = await h.Approvals.CreateAsync(new ApprovalRequest { RequestTitle = "Capex", CreatedBy = "creator" });
        var (claude, prompts) = Mocks();
        claude.SetupSequence(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("not json")
            .ReturnsAsync(ValidReadinessJson);

        var result = await Service(h, claude, prompts, maxRetries: 2).CheckReadinessAsync(created.Id);

        Assert.False(result.IsLikelyReady);
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CheckReadiness_NeverWritesToTheDatabase()
    {
        var h = await NewHarnessAsync();
        var created = await h.Approvals.CreateAsync(new ApprovalRequest { RequestTitle = "Capex", CreatedBy = "creator" });
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ValidReadinessJson);

        await Service(h, claude, prompts).CheckReadinessAsync(created.Id);

        var after = await h.Db.ApprovalRequests.AsNoTracking().SingleAsync(a => a.Id == created.Id);
        Assert.Equal("PendingApproval", after.WorkflowStatus);
        Assert.Equal(1, await h.Db.ApprovalCycles.CountAsync(c => c.ApprovalRequestId == created.Id));
    }

    // ============================================================
    // Approver suggestion — no directory exists, so this must never invent a name
    // ============================================================

    [Fact]
    public async Task RecommendApprover_NoDepartment_ReturnsNullWithoutCallingClaude()
    {
        var h = await NewHarnessAsync();
        var created = await h.Approvals.CreateAsync(new ApprovalRequest { RequestTitle = "No dept", CreatedBy = "creator" });
        var (claude, prompts) = Mocks();

        var result = await Service(h, claude, prompts).RecommendApproverAsync(created.Id);

        Assert.Null(result.RecommendedApproverName);
        Assert.Equal(0, result.HistoricalSampleSize);
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecommendApprover_NoHistoryForDepartment_ReturnsNullWithoutCallingClaude()
    {
        var h = await NewHarnessAsync();
        var created = await h.Approvals.CreateAsync(new ApprovalRequest { RequestTitle = "Fresh dept", Department = "NeverApprovedBefore", CreatedBy = "creator" });
        var (claude, prompts) = Mocks();

        var result = await Service(h, claude, prompts).RecommendApproverAsync(created.Id);

        Assert.Null(result.RecommendedApproverName);
        Assert.Equal(0, result.HistoricalSampleSize);
        Assert.Contains("No approved requests", result.Reasoning);
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecommendApprover_WithHistory_HonorsAiPick_WhenItIsARealCandidate()
    {
        var h = await NewHarnessAsync();
        // Two prior Approved requests in "Ops": Priya (2x), Ramesh (1x).
        foreach (var approver in new[] { "Priya", "Priya", "Ramesh" })
        {
            var req = await h.Approvals.CreateAsync(new ApprovalRequest { RequestTitle = "Past", Department = "Ops", CreatedBy = "creator" });
            await h.Lifecycle.ApproveAsync(req.Id, new ApprovalDecisionDto { Comment = "ok", EmployeeName = approver });
        }
        var current = await h.Approvals.CreateAsync(new ApprovalRequest { RequestTitle = "New capex", Department = "Ops", CreatedBy = "creator" });
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"recommendedApprover":"Ramesh","reasoning":"Ramesh has handled similar smaller requests."}""");

        var result = await Service(h, claude, prompts).RecommendApproverAsync(current.Id);

        Assert.Equal("Ramesh", result.RecommendedApproverName); // AI's real, valid pick is honored — not forced to top-by-count
        Assert.Equal(3, result.HistoricalSampleSize);
        Assert.Equal("Ramesh has handled similar smaller requests.", result.Reasoning);
    }

    [Fact]
    public async Task RecommendApprover_AiReturnsNameNotInCandidateList_FallsBackToTopCandidate()
    {
        var h = await NewHarnessAsync();
        foreach (var approver in new[] { "Priya", "Priya", "Ramesh" })
        {
            var req = await h.Approvals.CreateAsync(new ApprovalRequest { RequestTitle = "Past", Department = "Ops", CreatedBy = "creator" });
            await h.Lifecycle.ApproveAsync(req.Id, new ApprovalDecisionDto { Comment = "ok", EmployeeName = approver });
        }
        var current = await h.Approvals.CreateAsync(new ApprovalRequest { RequestTitle = "New capex", Department = "Ops", CreatedBy = "creator" });
        var (claude, prompts) = Mocks();
        // Hallucinated name that was never a real candidate — must never surface as-is.
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"recommendedApprover":"SomeoneNeverSeen","reasoning":"Best fit."}""");

        var result = await Service(h, claude, prompts).RecommendApproverAsync(current.Id);

        Assert.Equal("Priya", result.RecommendedApproverName); // falls back to the most-frequent REAL candidate
    }

    [Fact]
    public async Task RecommendApprover_UnknownApproval_ThrowsNotFound()
    {
        var h = await NewHarnessAsync();
        var (claude, prompts) = Mocks();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(h, claude, prompts).RecommendApproverAsync(999999));
    }

    // ============================================================
    // Status summary
    // ============================================================

    [Fact]
    public async Task SummarizeStatus_ReturnsAiSummary_AndMirrorsRealWorkflowFields()
    {
        var h = await NewHarnessAsync();
        var created = await h.Approvals.CreateAsync(new ApprovalRequest { RequestTitle = "Capex", CreatedBy = "creator" });
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ValidStatusJson);

        var result = await Service(h, claude, prompts).SummarizeStatusAsync(created.Id);

        Assert.Equal("Pending initial approval, no changes requested yet.", result.Summary);
        Assert.Equal("PendingApproval", result.WorkflowStatus);
        Assert.Equal(1, result.CurrentCycleNo);
    }

    [Fact]
    public async Task SummarizeStatus_UnknownApproval_ThrowsNotFound()
    {
        var h = await NewHarnessAsync();
        var (claude, prompts) = Mocks();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(h, claude, prompts).SummarizeStatusAsync(999999));
    }

    [Fact]
    public async Task SummarizeStatus_MalformedJson_ExhaustsRetries_Throws()
    {
        var h = await NewHarnessAsync();
        var created = await h.Approvals.CreateAsync(new ApprovalRequest { RequestTitle = "Capex", CreatedBy = "creator" });
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("still not json");

        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(h, claude, prompts, maxRetries: 2).SummarizeStatusAsync(created.Id));
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
