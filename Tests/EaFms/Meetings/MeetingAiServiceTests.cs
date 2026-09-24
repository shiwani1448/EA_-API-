using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.Ai;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Meetings;

/// <summary>
/// Phase 2 (preview only): POST /api/ea/meetings/{meetingId}/ai/analyze. Runs against an
/// in-memory EaFmsDbContext with IClaudeClient/IDocumentExtractionService/
/// IMeetingCompletionFileStore mocked — never calls the real Anthropic API.
/// </summary>
public class MeetingAiServiceTests
{
    private const string ValidJson = """{"proposedActions":[{"title":"Send report","description":"Send the Q3 report","doerName":"Anita","priority":"High","dueDate":"2026-10-01"}]}""";

    private static EaFmsDbContext NewDb() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static long SeedMeeting(EaFmsDbContext db, string? mom = null, long? pdfAttachmentId = null)
    {
        var meeting = new Meeting
        {
            Title = "AI test meeting",
            DoerIds = Array.Empty<string>(),
            DoerNames = Array.Empty<string>(),
            CompletionMom = mom,
            CompletionPdfAttachmentId = pdfAttachmentId,
            CreatedBy = "seed",
            CreatedDate = DateTime.UtcNow,
        };
        db.Meetings.Add(meeting);
        db.SaveChanges();
        return meeting.Id;
    }

    private static long SeedAttachment(EaFmsDbContext db, bool active = true)
    {
        var a = new Attachment
        {
            RelatedModule = "Meeting",
            RelatedEntity = "Meeting",
            OriginalFileName = "completion.pdf",
            ObjectKey = "Content/MeetingCompletion/1/x.pdf",
            ContentType = "application/pdf",
            UploadedBy = "seed",
            UploadedAt = DateTime.UtcNow,
            IsActive = active,
            CreatedBy = "seed",
            CreatedDate = DateTime.UtcNow,
        };
        db.Attachments.Add(a);
        db.SaveChanges();
        return a.Id;
    }

    private static (Mock<IClaudeClient> Claude, Mock<IMeetingActionExtractionPromptBuilder> Prompts,
        Mock<hrms_api.Services.IDocumentExtractionService> Extraction, Mock<IMeetingCompletionFileStore> Files) Mocks()
    {
        var claude = new Mock<IClaudeClient>();
        var prompts = new Mock<IMeetingActionExtractionPromptBuilder>();
        prompts.Setup(p => p.BuildSystemPrompt()).Returns("system");
        prompts.Setup(p => p.BuildUserPrompt(It.IsAny<string?>(), It.IsAny<string?>())).Returns("user");
        var extraction = new Mock<hrms_api.Services.IDocumentExtractionService>();
        var files = new Mock<IMeetingCompletionFileStore>();
        files.Setup(f => f.Resolve(It.IsAny<string>())).Returns((string k) => k);
        return (claude, prompts, extraction, files);
    }

    // MeetingAiService resolves IClaudeClient lazily via IServiceProvider (see
    // MeetingAiService's own comment) rather than taking it as a direct constructor
    // dependency, so tests hand it a stub provider that resolves to the mock.
    private static IServiceProvider MakeServiceProvider(IClaudeClient claude)
    {
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(IClaudeClient))).Returns(claude);
        return sp.Object;
    }

    private static MeetingAiService Service(
        EaFmsDbContext db,
        Mock<IClaudeClient> claude,
        Mock<IMeetingActionExtractionPromptBuilder> prompts,
        Mock<hrms_api.Services.IDocumentExtractionService> extraction,
        Mock<IMeetingCompletionFileStore> files,
        int maxRetries = 2) =>
        new(db, MakeServiceProvider(claude.Object), prompts.Object, extraction.Object, files.Object,
            NullLogger<MeetingAiService>.Instance,
            Options.Create(new ClaudeOptions { MaxRetries = maxRetries }));

    // 1. MOM only
    [Fact]
    public async Task MomOnly_Analyzes_AndReportsMomUsed()
    {
        await using var db = NewDb();
        var id = SeedMeeting(db, mom: "Discussed budget. Anita to send report.");
        var (claude, prompts, extraction, files) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ValidJson);

        var result = await Service(db, claude, prompts, extraction, files).AnalyzeAsync(id);

        Assert.True(result.MomUsed);
        Assert.False(result.PdfUsed);
        Assert.Null(result.WarningMessage);
        var action = Assert.Single(result.ProposedActions);
        Assert.Equal("Send report", action.Title);
        Assert.Equal("Anita", action.DoerName);
        extraction.Verify(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Full audit trail: every generated suggestion is logged verbatim into its own
    // per-module table, mirroring SCIH's separate SCIH_Analysis/SCIH_SolutionDesign tables
    // — see MeetingActionExtraction's own doc comment.
    [Fact]
    public async Task Analyze_WritesMeetingActionExtractionRow()
    {
        await using var db = NewDb();
        var id = SeedMeeting(db, mom: "Discussed budget. Anita to send report.");
        var (claude, prompts, extraction, files) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ValidJson);

        await Service(db, claude, prompts, extraction, files).AnalyzeAsync(id);

        var log = Assert.Single(db.MeetingActionExtractions);
        Assert.Equal(id, log.MeetingId);
        Assert.True(log.MomUsed);
        Assert.False(log.IsApplied);
        Assert.Contains("Send report", log.ProposedActionsJson);
    }

    // 2. PDF only
    [Fact]
    public async Task PdfOnly_ExtractsAndAnalyzes_AndReportsPdfUsed()
    {
        await using var db = NewDb();
        var attachmentId = SeedAttachment(db);
        var id = SeedMeeting(db, pdfAttachmentId: attachmentId);
        var (claude, prompts, extraction, files) = Mocks();
        extraction.Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hrms_api.Services.DocumentExtractionResult.Ok("Extracted PDF text: Anita to send report.", "TextLayer"));
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ValidJson);

        var result = await Service(db, claude, prompts, extraction, files).AnalyzeAsync(id);

        Assert.False(result.MomUsed);
        Assert.True(result.PdfUsed);
        Assert.Null(result.WarningMessage);
        prompts.Verify(p => p.BuildUserPrompt(null, "Extracted PDF text: Anita to send report."), Times.Once);
    }

    // 3. MOM + PDF
    [Fact]
    public async Task MomAndPdf_AnalyzesBoth()
    {
        await using var db = NewDb();
        var attachmentId = SeedAttachment(db);
        var id = SeedMeeting(db, mom: "Mom text.", pdfAttachmentId: attachmentId);
        var (claude, prompts, extraction, files) = Mocks();
        extraction.Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hrms_api.Services.DocumentExtractionResult.Ok("Pdf text.", "TextLayer"));
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ValidJson);

        var result = await Service(db, claude, prompts, extraction, files).AnalyzeAsync(id);

        Assert.True(result.MomUsed);
        Assert.True(result.PdfUsed);
        prompts.Verify(p => p.BuildUserPrompt("Mom text.", "Pdf text."), Times.Once);
    }

    // 4. neither MOM nor PDF
    [Fact]
    public async Task NeitherMomNorPdf_ThrowsBusinessRuleException()
    {
        await using var db = NewDb();
        var id = SeedMeeting(db);
        var (claude, prompts, extraction, files) = Mocks();

        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db, claude, prompts, extraction, files).AnalyzeAsync(id));
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 5. PDF extraction failure + usable MOM
    [Fact]
    public async Task PdfExtractionFailure_WithUsableMom_ContinuesWithMom_AndWarns()
    {
        await using var db = NewDb();
        var attachmentId = SeedAttachment(db);
        var id = SeedMeeting(db, mom: "Mom text.", pdfAttachmentId: attachmentId);
        var (claude, prompts, extraction, files) = Mocks();
        extraction.Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hrms_api.Services.DocumentExtractionResult.Fail("OCR failed"));
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ValidJson);

        var result = await Service(db, claude, prompts, extraction, files).AnalyzeAsync(id);

        Assert.True(result.MomUsed);
        Assert.False(result.PdfUsed);
        Assert.NotNull(result.WarningMessage);
        Assert.Contains("Completion PDF could not be read", result.WarningMessage);
    }

    // 6. PDF extraction failure + no MOM
    [Fact]
    public async Task PdfExtractionFailure_WithNoMom_FailsClearly()
    {
        await using var db = NewDb();
        var attachmentId = SeedAttachment(db);
        var id = SeedMeeting(db, pdfAttachmentId: attachmentId);
        var (claude, prompts, extraction, files) = Mocks();
        extraction.Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hrms_api.Services.DocumentExtractionResult.Fail("OCR failed"));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db, claude, prompts, extraction, files).AnalyzeAsync(id));
        Assert.Contains("No usable meeting completion evidence", ex.Message);
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 7. valid Claude JSON maps every field, nulls preserved (nothing invented)
    [Fact]
    public async Task ValidClaudeJson_MapsAllFields_AndAllowsNulls()
    {
        await using var db = NewDb();
        var id = SeedMeeting(db, mom: "text");
        var (claude, prompts, extraction, files) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"proposedActions":[{"title":"Only title, nothing else"}]}""");

        var result = await Service(db, claude, prompts, extraction, files).AnalyzeAsync(id);

        var a = Assert.Single(result.ProposedActions);
        Assert.Equal("Only title, nothing else", a.Title);
        Assert.Null(a.Description);
        Assert.Null(a.DoerName);
        Assert.Null(a.Priority);
        Assert.Null(a.DueDate);
    }

    // 8. malformed Claude JSON retries, then succeeds
    [Fact]
    public async Task MalformedJson_RetriesThenSucceeds()
    {
        await using var db = NewDb();
        var id = SeedMeeting(db, mom: "text");
        var (claude, prompts, extraction, files) = Mocks();
        claude.SetupSequence(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("not json at all")
            .ReturnsAsync(ValidJson);

        var result = await Service(db, claude, prompts, extraction, files, maxRetries: 2).AnalyzeAsync(id);

        Assert.Single(result.ProposedActions);
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task MalformedJson_ExhaustsConfiguredRetries_ThenThrows()
    {
        await using var db = NewDb();
        var id = SeedMeeting(db, mom: "text");
        var (claude, prompts, extraction, files) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("still not json");

        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db, claude, prompts, extraction, files, maxRetries: 2).AnalyzeAsync(id));
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // 9. proposed doerName is plain text only — no DoerId field exists to invent into
    [Fact]
    public void ProposedActionDto_HasNoDoerIdOrEmployeeIdentityField()
    {
        var props = typeof(MeetingAiProposedActionDto).GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("DoerId", props);
        Assert.Contains("DoerName", props);
    }

    // 10, 11, 12, 13, 15. no MeetingAction/Delegation/EaTask/WorkflowInstance created;
    // Meeting status and completion evidence fields unchanged (nothing is persisted at all)
    [Fact]
    public async Task Analysis_WritesNothingToTheDatabase()
    {
        await using var db = NewDb();
        var attachmentId = SeedAttachment(db);
        var id = SeedMeeting(db, mom: "Mom text.", pdfAttachmentId: attachmentId);
        var before = await db.Meetings.AsNoTracking().SingleAsync(m => m.Id == id);
        var (claude, prompts, extraction, files) = Mocks();
        extraction.Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hrms_api.Services.DocumentExtractionResult.Ok("Pdf text.", "TextLayer"));
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ValidJson);

        await Service(db, claude, prompts, extraction, files).AnalyzeAsync(id);

        Assert.Equal(0, await db.MeetingActions.CountAsync());
        Assert.Equal(0, await db.Delegations.CountAsync());
        Assert.Equal(0, await db.Tasks.CountAsync());
        Assert.Equal(0, await db.WorkflowInstances.CountAsync());
        Assert.Equal(1, await db.Attachments.CountAsync()); // still just the one seeded attachment

        var after = await db.Meetings.AsNoTracking().SingleAsync(m => m.Id == id);
        Assert.Equal(before.CompletionMom, after.CompletionMom);
        Assert.Equal(before.CompletionPdfAttachmentId, after.CompletionPdfAttachmentId);
        Assert.Equal(before.StatusId, after.StatusId);
        Assert.Equal(before.CompletedAt, after.CompletedAt);
    }

    // 16. unknown meeting
    [Fact]
    public async Task UnknownMeeting_ThrowsNotFound()
    {
        await using var db = NewDb();
        var (claude, prompts, extraction, files) = Mocks();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db, claude, prompts, extraction, files).AnalyzeAsync(999));
    }

    // 17. response schema — exactly the documented shape, nothing more
    [Fact]
    public void ResponseDtos_HaveExactlyTheDocumentedShape()
    {
        var responseProps = typeof(MeetingAiAnalysisResponseDto).GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "MeetingId", "MomUsed", "PdfUsed", "ProposedActions", "WarningMessage" }, responseProps);

        var actionProps = typeof(MeetingAiProposedActionDto).GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "Description", "DoerName", "DueDate", "Priority", "Title" }, actionProps);
    }
}
