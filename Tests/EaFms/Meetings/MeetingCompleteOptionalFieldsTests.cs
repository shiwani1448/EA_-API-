using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Controllers;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Jarvis5.Tests.EaFms.Delegation;
using Jarvis5.Validators;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UglyToad.PdfPig.Writer;
using Xunit;
using static Jarvis5.Tests.EaFms.Followups.FollowupBusinessApiTests;

namespace Jarvis5.Tests.EaFms.Meetings;

/// <summary>
/// POST /api/ea/meetings/{meetingId}/complete: completionMom and completionPdf are optional (the frontend controls mandatory fields).
/// A supplied PDF still gets the full existing validation. Runs the real Meeting lifecycle and shared workflow engine against a
/// throwaway PostgreSQL database (see ScratchTatDatabase), so nothing is written to the developer database.
/// </summary>
public class MeetingCompleteOptionalFieldsTests : IClassFixture<ScratchTatDatabase>, IDisposable
{
    private readonly ScratchTatDatabase _fx;
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private static readonly ICurrentUserService User = Mock.Of<ICurrentUserService>(u => u.UserName == "mtg-actor" && u.UserId == 9L);

    public MeetingCompleteOptionalFieldsTests(ScratchTatDatabase fx)
    {
        _fx = fx;
        Directory.CreateDirectory(_root);
    }

    public void Dispose() { try { Directory.Delete(_root, true); } catch { /* best-effort */ } }

    private MeetingLifecycleService NewService(EaFmsDbContext db)
    {
        var env = Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(e => e.ContentRootPath == _root);
        var audit = new AuditService(db, User);
        var wfRepo = new WorkflowRepository(db);
        var workflows = new WorkflowService(wfRepo, db, Mapper, User, audit);
        var execution = new WorkflowExecutionService(db, workflows, wfRepo, User, audit, Mapper);
        var eaTasks = new EaTaskService(db, new EaTaskRepository(db), new TatRuleRepository(db), new CreateEaTaskDtoValidator(), User, audit);
        var taskReview = new TaskReviewService(db, new TaskReviewRepository(db), User, audit);
        var delegations = new DelegationService(db, User, audit, new DelegationRepository(db), eaTasks, env, taskReview, new TatRuleRepository(db));
        var meetings = new Mock<IMeetingService>();
        meetings.Setup(m => m.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(new MeetingDetailResponseDto { TatSummary = new MeetingTatSummaryDto() });
        return new MeetingLifecycleService(db, execution, User, audit, meetings.Object, new MeetingCompletionFileStore(env), delegations, NullLogger<MeetingLifecycleService>.Instance);
    }

    /// <summary>A Meeting with a Captured workflow and a TAT-snapshotted EaTask, wired the way MeetingService.CreateAsync wires them.</summary>
    private async Task<long> SeedMeetingAsync()
    {
        await using var db = _fx.Db();
        var captured = await db.Statuses.SingleAsync(s => s.Name == "Captured");
        var meeting = new Meeting { Title = "Optional-field meeting", MeetingNumber = "MTG-" + Guid.NewGuid().ToString("N")[..8], DoerIds = Array.Empty<string>(), DoerNames = Array.Empty<string>(), CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();
        var wf = new WorkflowInstance { BusinessModuleId = _fx.MeetingModuleId, BusinessRecordId = meeting.Id.ToString(), StatusId = captured.Id, StartedAt = DateTime.UtcNow, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.WorkflowInstances.Add(wf);
        await db.SaveChangesAsync();
        meeting.WorkflowInstanceId = wf.Id;
        db.Tasks.Add(new EaTask
        {
            BusinessModuleId = _fx.MeetingModuleId, ModuleName = "Meeting", BusinessRecordId = meeting.Id.ToString(), Task = meeting.Title,
            Type = "Internal", Subtype = "Review", AllottedTatMinutes = 60, ExecutionStatus = EaTaskExecutionStatus.NotStarted,
            WorkflowInstanceId = wf.Id, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return meeting.Id;
    }

    private async Task<long> SeedStartedMeetingAsync()
    {
        var id = await SeedMeetingAsync();
        await using var db = _fx.Db();
        await NewService(db).StartAsync(id, new MeetingStartRequestDto(), default);
        return id;
    }

    private static IFormFile Pdf(string name = "mom.pdf")
    {
        var b = new PdfDocumentBuilder();
        b.AddPage(200, 200);
        var ms = new MemoryStream(b.Build());
        return new FormFile(ms, 0, ms.Length, "completionPdf", name) { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
    }

    private static IFormFile Bytes(byte[] data, string name, string contentType)
    {
        var ms = new MemoryStream(data);
        return new FormFile(ms, 0, ms.Length, "completionPdf", name) { Headers = new HeaderDictionary(), ContentType = contentType };
    }

    private async Task<MeetingLifecycleResponseDto> CompleteAsync(long meetingId, MeetingCompleteRequestDto dto)
    {
        await using var db = _fx.Db();
        return await NewService(db).CompleteAsync(meetingId, dto, default);
    }

    private async Task AssertCompletedCentrallyAsync(long meetingId)
    {
        await using var db = _fx.Db();
        var meeting = await db.Meetings.AsNoTracking().SingleAsync(m => m.Id == meetingId);
        Assert.NotNull(meeting.CompletedAt);
        var task = await db.Tasks.AsNoTracking().SingleAsync(t => t.BusinessModuleId == _fx.MeetingModuleId && t.BusinessRecordId == meetingId.ToString());
        Assert.Equal(EaTaskExecutionStatus.Completed, task.ExecutionStatus);
        Assert.NotNull(task.CompletedAt);
        Assert.NotNull(task.TatUsedMinutes);   // TAT frozen exactly as before
        var wf = await db.WorkflowInstances.AsNoTracking().SingleAsync(w => w.Id == meeting.WorkflowInstanceId);
        Assert.NotNull(wf.CompletedAt);
    }

    // ---------------- A-D ----------------
    [Fact]
    public async Task A_NoMomNoPdf_Completes_WithNullMomAndNoAttachment()
    {
        var id = await SeedStartedMeetingAsync();

        var result = await CompleteAsync(id, new MeetingCompleteRequestDto());

        Assert.NotNull(result);
        await AssertCompletedCentrallyAsync(id);
        await using var db = _fx.Db();
        var meeting = await db.Meetings.AsNoTracking().SingleAsync(m => m.Id == id);
        Assert.Null(meeting.CompletionMom);
        Assert.Null(meeting.CompletionPdfAttachmentId);
        Assert.False(await db.Attachments.AnyAsync(a => a.RelatedModule == "Meeting" && a.RelatedEntityId == id.ToString()));
        Assert.False(Directory.Exists(Path.Combine(_root, "Content", "MeetingCompletion", id.ToString())));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A2_BlankMom_IsStoredAsNull_AndCompletionSucceeds(string blank)
    {
        var id = await SeedStartedMeetingAsync();

        await CompleteAsync(id, new MeetingCompleteRequestDto { CompletionMom = blank });

        await using var db = _fx.Db();
        Assert.Null((await db.Meetings.AsNoTracking().SingleAsync(m => m.Id == id)).CompletionMom);
        await AssertCompletedCentrallyAsync(id);
    }

    [Fact]
    public async Task B_MomOnly_Completes_AndStoresTheTrimmedMom_WithoutAnAttachment()
    {
        var id = await SeedStartedMeetingAsync();

        await CompleteAsync(id, new MeetingCompleteRequestDto { CompletionMom = "  Decisions recorded.  " });

        await AssertCompletedCentrallyAsync(id);
        await using var db = _fx.Db();
        var meeting = await db.Meetings.AsNoTracking().SingleAsync(m => m.Id == id);
        Assert.Equal("Decisions recorded.", meeting.CompletionMom);
        Assert.Null(meeting.CompletionPdfAttachmentId);
    }

    [Fact]
    public async Task C_PdfOnly_Completes_AndPersistsTheAttachmentAndFile()
    {
        var id = await SeedStartedMeetingAsync();

        await CompleteAsync(id, new MeetingCompleteRequestDto { CompletionPdf = Pdf() });

        await AssertCompletedCentrallyAsync(id);
        await using var db = _fx.Db();
        var meeting = await db.Meetings.AsNoTracking().SingleAsync(m => m.Id == id);
        Assert.Null(meeting.CompletionMom);
        var attachment = await db.Attachments.AsNoTracking().SingleAsync(a => a.Id == meeting.CompletionPdfAttachmentId);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.True(File.Exists(Path.Combine(_root, attachment.ObjectKey!.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task D_MomAndPdf_Completes_AndStoresBoth()
    {
        var id = await SeedStartedMeetingAsync();

        await CompleteAsync(id, new MeetingCompleteRequestDto { CompletionMom = "Full minutes.", CompletionPdf = Pdf() });

        await AssertCompletedCentrallyAsync(id);
        await using var db = _fx.Db();
        var meeting = await db.Meetings.AsNoTracking().SingleAsync(m => m.Id == id);
        Assert.Equal("Full minutes.", meeting.CompletionMom);
        Assert.NotNull(meeting.CompletionPdfAttachmentId);
    }

    // ---------------- E: a supplied invalid PDF is still rejected ----------------
    public static TheoryData<string, string, string, string> InvalidPdfs => new()
    {
        { "not-a-pdf.txt", "text/plain", "hello", "Completion file must be a PDF." },
        { "fake.pdf", "application/pdf", "this is not a pdf at all", "Completion PDF signature is invalid." },
        { "broken.pdf", "application/pdf", "%PDF-1.4 truncated garbage", "Completion file is not a readable PDF." },
    };

    [Theory]
    [MemberData(nameof(InvalidPdfs))]
    public async Task E_InvalidSuppliedPdf_IsRejected_ByTheExistingValidation_AndNothingIsCompleted(string name, string contentType, string content, string message)
    {
        var id = await SeedStartedMeetingAsync();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CompleteAsync(id, new MeetingCompleteRequestDto
        { CompletionMom = "ignored", CompletionPdf = Bytes(System.Text.Encoding.ASCII.GetBytes(content), name, contentType) }));

        Assert.Equal(message, ex.Message);
        await using var db = _fx.Db();
        var meeting = await db.Meetings.AsNoTracking().SingleAsync(m => m.Id == id);
        Assert.Null(meeting.CompletedAt);
        Assert.Null(meeting.CompletionMom);
        Assert.Equal(EaTaskExecutionStatus.InProgress, (await db.Tasks.AsNoTracking().SingleAsync(t => t.BusinessRecordId == id.ToString() && t.ModuleName == "Meeting")).ExecutionStatus);
    }

    [Fact]
    public async Task E_EmptyAndOversizedSuppliedPdf_AreRejected()
    {
        var id = await SeedStartedMeetingAsync();

        var empty = await Assert.ThrowsAsync<BusinessRuleException>(() => CompleteAsync(id, new MeetingCompleteRequestDto { CompletionPdf = Bytes(Array.Empty<byte>(), "e.pdf", "application/pdf") }));
        var big = await Assert.ThrowsAsync<BusinessRuleException>(() => CompleteAsync(id, new MeetingCompleteRequestDto { CompletionPdf = Bytes(new byte[MeetingCompletionFileStore.MaxBytes + 1], "b.pdf", "application/pdf") }));

        Assert.Contains("nonempty completion PDF of at most 25 MiB", empty.Message);
        Assert.Contains("nonempty completion PDF of at most 25 MiB", big.Message);
    }

    [Fact]
    public async Task SuppliedMom_OverTheColumnSize_IsStillRejected()
    {
        var id = await SeedStartedMeetingAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => CompleteAsync(id, new MeetingCompleteRequestDto { CompletionMom = new string('x', 4001) }));
        Assert.True(new MeetingCompleteRequestDtoValidator().Validate(new MeetingCompleteRequestDto { CompletionMom = new string('x', 4000) }).IsValid);
        Assert.False(new MeetingCompleteRequestDtoValidator().Validate(new MeetingCompleteRequestDto { CompletionMom = new string('x', 4001) }).IsValid);
    }

    // ---------------- lifecycle unchanged ----------------
    [Fact]
    public async Task Start_Pause_Resume_Complete_TatAndEaTask_StillBehaveAsBefore_WithNoMomOrPdf()
    {
        var id = await SeedMeetingAsync();
        await using (var db = _fx.Db())
        {
            var started = await NewService(db).StartAsync(id, new MeetingStartRequestDto(), default);
            Assert.False(started.IsPaused);
        }
        await using (var db = _fx.Db())
            Assert.True((await NewService(db).PauseAsync(id, new MeetingPauseRequestDto(), default)).IsPaused);

        // completing while paused is still blocked, exactly as before
        var blocked = await Assert.ThrowsAsync<BusinessRuleException>(() => CompleteAsync(id, new MeetingCompleteRequestDto()));
        Assert.Equal("Resume or continue open pauses/waiting before completion.", blocked.Message);

        await using (var db = _fx.Db())
            Assert.False((await NewService(db).ResumeAsync(id, new MeetingResumeRequestDto(), default)).IsPaused);
        await CompleteAsync(id, new MeetingCompleteRequestDto());

        await AssertCompletedCentrallyAsync(id);
        await using var verify = _fx.Db();
        var task = await verify.Tasks.AsNoTracking().SingleAsync(t => t.BusinessRecordId == id.ToString() && t.ModuleName == "Meeting");
        Assert.Equal(60, task.AllottedTatMinutes);                       // snapshot untouched
        Assert.Equal("Internal", task.Type);
        Assert.Equal("Review", task.Subtype);                            // Meeting stays Type + Subtype
        Assert.Equal(1, await verify.WorkPauses.CountAsync(p => p.WorkflowInstanceId == task.WorkflowInstanceId && p.EndAt != null));
    }

    [Fact]
    public async Task CompleteBeforeStart_UnknownMeeting_AndDoubleComplete_KeepTheirExistingRejections()
    {
        var notStarted = await SeedMeetingAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => CompleteAsync(notStarted, new MeetingCompleteRequestDto()));
        await Assert.ThrowsAsync<NotFoundException>(() => CompleteAsync(987654321, new MeetingCompleteRequestDto()));

        var started = await SeedStartedMeetingAsync();
        await CompleteAsync(started, new MeetingCompleteRequestDto());
        await Assert.ThrowsAsync<BusinessRuleException>(() => CompleteAsync(started, new MeetingCompleteRequestDto()));
    }

    // ---------------- contract ----------------
    [Fact]
    public void Contract_BothFieldsAreOptional_MeetingIdStaysARoutePathParameter_RouteUnchanged()
    {
        var dto = typeof(MeetingCompleteRequestDto);
        var nullability = new NullabilityInfoContext();
        foreach (var name in new[] { nameof(MeetingCompleteRequestDto.CompletionMom), nameof(MeetingCompleteRequestDto.CompletionPdf) })
        {
            var p = dto.GetProperty(name)!;
            Assert.Null(p.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>());
            Assert.Equal(NullabilityState.Nullable, nullability.Create(p).WriteState);
        }
        Assert.Equal("completionMom", dto.GetProperty(nameof(MeetingCompleteRequestDto.CompletionMom))!.GetCustomAttribute<FromFormAttribute>()!.Name);
        Assert.Equal("completionPdf", dto.GetProperty(nameof(MeetingCompleteRequestDto.CompletionPdf))!.GetCustomAttribute<FromFormAttribute>()!.Name);

        var action = typeof(MeetingsLifecycleController).GetMethod(nameof(MeetingsLifecycleController.Complete))!;
        Assert.Equal("complete", action.GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.Contains(action.GetCustomAttributes(), a => a is ConsumesAttribute c && c.ContentTypes.Contains("multipart/form-data"));
        Assert.Equal(typeof(long), action.GetParameters().Single(p => p.Name == "meetingId").ParameterType);   // non-nullable path id
    }

    [Fact]
    public void Validator_NoLongerRequiresEither_ButStillLimitsAMom()
    {
        var validator = new MeetingCompleteRequestDtoValidator();

        Assert.True(validator.Validate(new MeetingCompleteRequestDto()).IsValid);
        Assert.True(validator.Validate(new MeetingCompleteRequestDto { CompletionMom = "" }).IsValid);
        Assert.False(validator.Validate(new MeetingCompleteRequestDto { CompletionMom = new string('x', 4001) }).IsValid);
    }
}
