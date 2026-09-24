using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Common;
using Jarvis5.Controllers.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace Jarvis5.Tests.EaFms.Delegation;

/// <summary>
/// POST /api/ea/delegations/{id}/complete takes an optional multipart <c>completionPdf</c> (same field name as
/// Meeting). The PDF is uploaded inside that same request and stored in ea_attachments in the same transaction
/// as the completion. There is no separate upload, download, document or evidence endpoint.
/// </summary>
public class DelegationCompletionPdfTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    public void Dispose()
    {
        foreach (var dir in _tempDirs) try { Directory.Delete(dir, recursive: true); } catch { /* best-effort */ }
    }

    private static byte[] ValidPdf()
    {
        var b = new PdfDocumentBuilder();
        b.AddPage(200, 200);
        return b.Build();
    }

    private static IFormFile File(string fileName, string contentType, byte[] data, string field = "completionPdf")
    {
        var ms = new MemoryStream(data);
        return new FormFile(ms, 0, ms.Length, field, fileName) { Headers = new HeaderDictionary(), ContentType = contentType };
    }

    private static IFormFile Pdf(string name = "completion.pdf") => File(name, "application/pdf", ValidPdf());

    private sealed class Fx
    {
        public required EaFmsDbContext Db { get; init; }
        public required DelegationService Svc { get; init; }
        public required string Root { get; init; }
        public required long DelegationId { get; init; }
        public required long EaTaskId { get; init; }
        public string CompletionFolder => Path.Combine(Root, "Content", "DelegationCompletion");
        public IEnumerable<string> Files => Directory.Exists(CompletionFolder)
            ? Directory.GetFiles(CompletionFolder, "*", SearchOption.AllDirectories) : Array.Empty<string>();
    }

    private async Task<Fx> NewAsync(Mock<IAuditService>? audit = null, string? dbName = null, bool start = true)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
        _tempDirs.Add(root);
        var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == root);
        var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
        db.BusinessModules.Add(new BusinessModule { Name = DelegationService.DelegationBusinessModuleName, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "ea-actor" && u.UserId == 42L);
        var numbers = new Mock<IDelegationNumberRepository>();
        var seq = 0;
        numbers.Setup(r => r.GenerateNextReferenceNoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => $"DLG-T-{++seq:D6}");
        var tasks = new Mock<IEaTaskService>();
        tasks.Setup(s => s.CreateWithoutTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateEaTaskDto dto, CancellationToken _) =>
            {
                var t = new EaTask { BusinessModuleId = 1, ModuleName = DelegationService.DelegationBusinessModuleName, BusinessRecordId = dto.BusinessRecordId,
                    Task = dto.Task ?? dto.BusinessRecordId, ExecutionStatus = "NotStarted", IsActive = true, CreatedBy = "ea-actor", CreatedDate = DateTime.UtcNow };
                db.Tasks.Add(t); db.SaveChanges();
                return new EaTaskResponseDto { EaTaskId = t.Id, ModuleId = 1, ModuleName = t.ModuleName, BusinessRecordId = t.BusinessRecordId,
                    Task = t.Task, ExecutionStatus = t.ExecutionStatus, IsActive = true, CreatedBy = t.CreatedBy, CreatedDate = t.CreatedDate };
            });
        var auditObj = (audit ?? new Mock<IAuditService>()).Object;
        var svc = new DelegationService(db, user, auditObj, numbers.Object, tasks.Object, env,
            new TaskReviewService(db, new TaskReviewRepository(db), user, auditObj), new TatRuleRepository(db));
        var created = await svc.CreateAsync(new DelegationCreateRequestDto { Title = "Prepare deck", DoerId = "emp-1", EndDate = DateTime.UtcNow.AddDays(5) });
        if (start) await svc.StartAsync(created.DelegationId);
        return new Fx { Db = db, Svc = svc, Root = root, DelegationId = created.DelegationId, EaTaskId = created.EaTaskId };
    }

    private static Task<List<Attachment>> Attachments(Fx f) => f.Db.Attachments.AsNoTracking().Where(a => a.RelatedModule == "Delegation").ToListAsync();

    // ---------------- contract ----------------
    [Fact]
    public void RequestContract_IsAMultipartCompletionPdfField_WithNoEvidenceProperty()
    {
        var prop = Assert.Single(typeof(DelegationCompleteRequestDto).GetProperties());
        var fromForm = prop.GetCustomAttribute<FromFormAttribute>();

        Assert.Equal("CompletionPdf", prop.Name);
        Assert.Equal(typeof(IFormFile), prop.PropertyType);
        Assert.Equal("completionPdf", fromForm!.Name);
        Assert.DoesNotContain(typeof(DelegationCompleteRequestDto).GetProperties(), p => p.Name.Contains("Evidence", StringComparison.OrdinalIgnoreCase));

        var complete = typeof(DelegationsController).GetMethod(nameof(DelegationsController.Complete))!;
        Assert.Contains(complete.GetCustomAttributes(), a => a is ConsumesAttribute c && c.ContentTypes.Contains("multipart/form-data"));
        Assert.Contains(complete.GetParameters(), p => p.ParameterType == typeof(DelegationCompleteRequestDto) && p.GetCustomAttribute<FromFormAttribute>() != null);
        Assert.DoesNotContain(complete.GetParameters(), p => p.ParameterType == typeof(IFormFile));
    }

    [Fact]
    public void PublicSurface_HasNoEvidenceOrCompletionPdfRoutes_AndTheUploadLivesInComplete()
    {
        var actions = typeof(DelegationsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes().OfType<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>()
                .Select(a => (m.Name, Verb: a.HttpMethods.Single(), a.Template))).ToList();

        Assert.DoesNotContain(actions, a => (a.Template ?? "").Contains("evidence", StringComparison.OrdinalIgnoreCase) || a.Name.Contains("Evidence"));
        Assert.DoesNotContain(actions, a => (a.Template ?? "").Contains("completion-pdf", StringComparison.OrdinalIgnoreCase) || a.Name.Contains("CompletionPdf"));
        Assert.DoesNotContain(actions, a => a.Name.Contains("Upload") || a.Name.Contains("Document") || a.Name.Contains("Attachment"));
        Assert.Equal(new[] { "GET ", "GET summary", "GET {delegationId:long}", "GET {delegationId:long}/review/history", "POST ", "POST {delegationId:long}/complete", "POST {delegationId:long}/pause", "POST {delegationId:long}/resume", "POST {delegationId:long}/review/approve", "POST {delegationId:long}/review/rework", "POST {delegationId:long}/review/start", "POST {delegationId:long}/rework/start", "POST {delegationId:long}/start", "POST {delegationId:long}/submit-for-review", "PUT {delegationId:long}" },
            actions.Select(a => $"{a.Verb} {a.Template}").OrderBy(x => x, StringComparer.Ordinal));
        Assert.Null(typeof(DelegationResponseDto).Assembly.GetType("Jarvis5.Dtos.EaFms.DelegationEvidenceDto"));
        Assert.DoesNotContain(typeof(IDelegationService).GetMethods(), m => m.Name.Contains("CompletionPdf") || m.Name.Contains("Evidence"));
    }

    [Fact]
    public void MeetingCompleteContract_KeepsTheFieldNames_ButBothFieldsAreNowOptional()
    {
        var pdf = typeof(MeetingCompleteRequestDto).GetProperty(nameof(MeetingCompleteRequestDto.CompletionPdf))!;

        Assert.Equal("completionPdf", pdf.GetCustomAttribute<FromFormAttribute>()!.Name);
        Assert.Null(pdf.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>());   // optional: the frontend controls mandatory fields
        Assert.Equal("completionMom", typeof(MeetingCompleteRequestDto).GetProperty(nameof(MeetingCompleteRequestDto.CompletionMom))!.GetCustomAttribute<FromFormAttribute>()!.Name);
    }

    // ---------------- optional PDF ----------------
    [Fact]
    public async Task CompleteWithoutPdf_StillCompletes_AndCreatesNoAttachment()
    {
        var f = await NewAsync();

        var result = await f.Svc.CompleteAndApproveAsync(f.DelegationId, null);

        Assert.Equal("Completed", result.Status);
        Assert.Null(result.CompletionPdfAttachmentId);
        Assert.Empty(await Attachments(f));
        Assert.Empty(f.Files);
    }

    // ---------------- happy path ----------------
    [Fact]
    public async Task CompleteWithPdf_CompletesDelegationAndTask_AndStoresOneAttachmentForThatDelegation()
    {
        var f = await NewAsync();
        var other = await f.Svc.CreateAsync(new DelegationCreateRequestDto { Title = "Other", DoerId = "emp-2" });

        var result = await f.Svc.CompleteAndApproveAsync(f.DelegationId, Pdf());

        Assert.Equal("Completed", result.Status);
        Assert.Equal("Completed", (await f.Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == f.EaTaskId)).ExecutionStatus);
        var att = Assert.Single(await Attachments(f));                       // exactly one, no duplicate
        Assert.Equal(("Delegation", "Delegation", f.DelegationId.ToString()), (att.RelatedModule, att.RelatedEntity, att.RelatedEntityId));
        Assert.Equal(("completion.pdf", "application/pdf"), (att.OriginalFileName, att.ContentType));
        Assert.Equal(att.Id, result.CompletionPdfAttachmentId);
        Assert.Null(att.AccessUrl);                                          // no storage path exposed
        Assert.StartsWith($"Content/DelegationCompletion/{f.DelegationId}/", att.ObjectKey);
        Assert.Single(f.Files);
        Assert.Equal("Pending", (await f.Db.Delegations.AsNoTracking().SingleAsync(d => d.Id == other.DelegationId)).Status);   // other Delegation untouched
    }

    [Fact]
    public async Task Complete_ThenCompleteAgain_IsRejected_AndDoesNotAddASecondAttachment()
    {
        var f = await NewAsync();
        await f.Svc.CompleteAsync(f.DelegationId, Pdf());

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.CompleteAsync(f.DelegationId, Pdf("again.pdf")));

        Assert.Single(await Attachments(f));
        Assert.Single(f.Files);
    }

    // ---------------- validation ----------------
    public static IEnumerable<object[]> InvalidUploads()
    {
        yield return new object[] { "photo.jpg", "image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x01 } };          // images are no longer accepted
        yield return new object[] { "screenshot.png", "image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D } };
        yield return new object[] { "malware.exe", "application/octet-stream", new byte[] { 1, 2, 3 } };
        yield return new object[] { "wrongtype.pdf", "image/png", ValidPdf() };                                       // content type must be application/pdf
        yield return new object[] { "fake.pdf", "application/pdf", "%PDF-not really a pdf"u8.ToArray() };            // signature ok, unreadable
        yield return new object[] { "text.pdf", "application/pdf", "hello world"u8.ToArray() };                        // bad signature
        yield return new object[] { "empty.pdf", "application/pdf", Array.Empty<byte>() };
    }

    [Theory]
    [MemberData(nameof(InvalidUploads))]
    public async Task InvalidUpload_IsRejected_AndNothingChanges(string name, string contentType, byte[] data)
    {
        var f = await NewAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.CompleteAsync(f.DelegationId, File(name, contentType, data)));

        Assert.Equal("InProgress", (await f.Db.Delegations.AsNoTracking().SingleAsync(d => d.Id == f.DelegationId)).Status);
        Assert.NotEqual("Completed", (await f.Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == f.EaTaskId)).ExecutionStatus);
        Assert.Empty(await Attachments(f));
        Assert.Empty(f.Files);
    }

    [Fact]
    public async Task OversizedAndLongNamedUploads_AreRejected_BeforeAnyChange()
    {
        var f = await NewAsync();
        var big = new Mock<IFormFile>();
        big.SetupGet(x => x.Length).Returns(26L * 1024 * 1024);
        big.SetupGet(x => x.FileName).Returns("big.pdf");
        big.SetupGet(x => x.ContentType).Returns("application/pdf");

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.CompleteAsync(f.DelegationId, big.Object));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Svc.CompleteAsync(f.DelegationId, File(new string('a', 500) + ".pdf", "application/pdf", ValidPdf())));

        Assert.Equal("InProgress", (await f.Db.Delegations.AsNoTracking().SingleAsync()).Status);
        Assert.Empty(await Attachments(f));
    }

    // ---------------- transaction / cleanup ----------------
    [Fact]
    public async Task CommitFailure_LeavesDelegationInProgress_NoAttachment_AndDeletesTheWrittenFile()
    {
        var name = Guid.NewGuid().ToString();
        var failNext = false;
        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.AddAudit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .Callback(() => { if (failNext) throw new InvalidOperationException("simulated failure after the file was written"); });
        var f = await NewAsync(audit, name);
        failNext = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Svc.CompleteAsync(f.DelegationId, Pdf()));

        Assert.Empty(f.Files);                                              // orphan file removed
        await using var fresh = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>().UseInMemoryDatabase(name)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
        Assert.Equal("InProgress", (await fresh.Delegations.AsNoTracking().SingleAsync()).Status);   // nothing was saved
        Assert.Empty(await fresh.Attachments.AsNoTracking().ToListAsync());
    }

    // ---------------- response metadata (no separate retrieval API) ----------------
    [Fact]
    public async Task DetailAndList_ExposeTheCompletionPdfAttachmentId_WithoutAnyPathOrUrl()
    {
        var f = await NewAsync();
        var done = await f.Svc.CompleteAsync(f.DelegationId, Pdf("final.pdf"));

        var detail = await f.Svc.GetByIdAsync(f.DelegationId);
        var list = await f.Svc.ListAsync(new DelegationListQueryDto());

        Assert.Equal(done.CompletionPdfAttachmentId, detail.CompletionPdfAttachmentId);
        Assert.Equal(done.CompletionPdfAttachmentId, Assert.Single(list.Items).CompletionPdfAttachmentId);
        Assert.DoesNotContain(typeof(DelegationResponseDto).GetProperties(), p => p.Name.Contains("Path") || p.Name.Contains("ObjectKey") || p.Name.Contains("Url"));
    }

    [Fact]
    public async Task DetailWithoutAPdf_HasNullAttachmentId_ForHistoricalAndPendingDelegations()
    {
        var f = await NewAsync();

        Assert.Null((await f.Svc.GetByIdAsync(f.DelegationId)).CompletionPdfAttachmentId);
    }

    [Fact]
    public async Task CompletionDoesNotTouchAttachmentsOfOtherModules()
    {
        var f = await NewAsync();
        f.Db.Attachments.Add(new Attachment { RelatedModule = "Meeting", RelatedEntity = "Meeting", RelatedEntityId = f.DelegationId.ToString(), OriginalFileName = "m.pdf",
            ObjectKey = "Content/MeetingCompletion/1/x.pdf", ContentType = "application/pdf", UploadedBy = "u", UploadedAt = DateTime.UtcNow, CreatedBy = "u", CreatedDate = DateTime.UtcNow, IsActive = true });
        await f.Db.SaveChangesAsync();

        var result = await f.Svc.CompleteAsync(f.DelegationId, Pdf());

        Assert.Single(await Attachments(f));
        Assert.Equal(1, await f.Db.Attachments.CountAsync(a => a.RelatedModule == "Meeting"));
        Assert.NotNull(result.CompletionPdfAttachmentId);
        Assert.Equal((await Attachments(f)).Single().Id, result.CompletionPdfAttachmentId);   // the Meeting attachment with the same entity id is not picked up
    }
}
