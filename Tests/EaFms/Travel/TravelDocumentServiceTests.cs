using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Travel;

/// <summary>
/// Focused tests for TravelDocumentService — Travel documents backed entirely by the
/// shared ea_attachments table (mirrors ApprovalDocumentServiceComprehensiveTests' style).
/// </summary>
public class TravelDocumentServiceTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    private static EaFmsDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private IWebHostEnvironment MakeEnv()
    {
        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(temp);
        _tempDirs.Add(temp);
        return Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == temp);
    }

    private static IFormFile CreateFormFile(byte[] data, string fileName, string contentType)
    {
        var ms = new MemoryStream(data);
        return new FormFile(ms, 0, ms.Length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = contentType };
    }

    // EA APIs run without JWT; UploadAsync resolves the actor via IEaActorResolver rather
    // than ICurrentUserService. These tests are about document upload/list/download/delete
    // behavior, not actor-identity resolution (that's TravelActorIdentityTests' job), so
    // any employeeId/employeeName (including the omitted/null defaults) resolves to a fixed name.
    private static TravelDocumentService MakeService(EaFmsDbContext db, IWebHostEnvironment env, string actor = "tester") =>
        new(db, Mock.Of<ICurrentUserService>(u => u.UserName == actor && u.UserId == 1), Mock.Of<IAuditService>(), env,
            Mock.Of<IEaActorResolver>(r =>
                r.ResolveDisplayNameAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult(actor)));

    private static TravelRequest AddTravel(
        EaFmsDbContext db, long id, long eaTaskId, string referenceNo,
        string businessState = "Draft", string approvalState = "NotRequired", int currentCycleNo = 0)
    {
        var entity = new TravelRequest
        {
            Id = id,
            ReferenceNo = referenceNo,
            EaTaskId = eaTaskId,
            BusinessState = businessState,
            ApprovalState = approvalState,
            CurrentCycleNo = currentCycleNo,
            CreatedBy = "tester",
            CreatedDate = DateTime.UtcNow
        };
        db.TravelRequests.Add(entity);
        db.SaveChanges();
        return entity;
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { Directory.Delete(dir, true); } catch { /* best-effort cleanup */ }
        }
    }

    // ----------------------------------------------------------------
    // 1. Upload to valid TravelRequest
    // ----------------------------------------------------------------
    [Fact]
    public async Task Upload_ToValidTravelRequest_PersistsAttachmentAndReturnsDto()
    {
        var db = CreateContext();
        var travel = AddTravel(db, 1, 100, "TRV-2026-000001");
        var svc = MakeService(db, MakeEnv());

        var dto = await svc.UploadAsync(1, CreateFormFile(new byte[] { 1, 2, 3 }, "itinerary.pdf", "application/pdf"), "Itinerary");

        Assert.Equal(1, dto.TravelRequestId);
        Assert.Equal("TRV-2026-000001", dto.TravelReferenceNo);
        Assert.Equal("itinerary.pdf", dto.OriginalFileName);
        Assert.Equal("Itinerary", dto.DocumentCategory);
        Assert.Null(dto.CycleNo); // Draft, CurrentCycleNo == 0 => no cycle association
        Assert.Equal("tester", dto.UploadedBy);
        Assert.StartsWith("/", dto.DownloadUrl);
        Assert.Equal($"/api/ea/travel/documents/{dto.Id}", dto.DownloadUrl);

        var att = db.Set<Attachment>().AsNoTracking().Single(a => a.Id == dto.Id);
        Assert.Equal("Travel", att.RelatedModule);
        Assert.Equal("TravelRequest", att.RelatedEntity);
        Assert.Equal("1", att.RelatedEntityId);
        Assert.False(att.IsDeleted);
        Assert.True(att.IsActive);
    }

    // ----------------------------------------------------------------
    // 2. Missing TravelRequest
    // ----------------------------------------------------------------
    [Fact]
    public async Task Upload_ToMissingTravelRequest_ThrowsNotFound()
    {
        var db = CreateContext();
        var svc = MakeService(db, MakeEnv());

        await Assert.ThrowsAsync<Jarvis5.Common.NotFoundException>(
            () => svc.UploadAsync(999, CreateFormFile(new byte[] { 1 }, "a.pdf", "application/pdf"), null));
        Assert.Empty(db.Set<Attachment>());
    }

    // ----------------------------------------------------------------
    // 3 & 9. List documents / multiple documents, newest first
    // ----------------------------------------------------------------
    [Fact]
    public async Task List_ReturnsMultipleDocuments_NewestFirst()
    {
        var db = CreateContext();
        AddTravel(db, 1, 100, "TRV-2026-000001");
        var svc = MakeService(db, MakeEnv());

        var first = await svc.UploadAsync(1, CreateFormFile(new byte[] { 1 }, "a.pdf", "application/pdf"), null);
        await Task.Delay(5); // ensure distinct UploadedAt ordering
        var second = await svc.UploadAsync(1, CreateFormFile(new byte[] { 2 }, "b.pdf", "application/pdf"), null);

        var list = await svc.ListAsync(1);

        Assert.Equal(2, list.Count);
        Assert.Equal(second.Id, list[0].Id);
        Assert.Equal(first.Id, list[1].Id);
    }

    // ----------------------------------------------------------------
    // 4. Document belongs only to the correct TravelRequest
    // ----------------------------------------------------------------
    [Fact]
    public async Task List_OnlyReturnsDocumentsForRequestedTravelRequestId()
    {
        var db = CreateContext();
        AddTravel(db, 1, 100, "TRV-2026-000001");
        AddTravel(db, 2, 101, "TRV-2026-000002");
        var svc = MakeService(db, MakeEnv());

        await svc.UploadAsync(1, CreateFormFile(new byte[] { 1 }, "for-1.pdf", "application/pdf"), null);
        await svc.UploadAsync(2, CreateFormFile(new byte[] { 2 }, "for-2.pdf", "application/pdf"), null);

        var listFor1 = await svc.ListAsync(1);

        Assert.Single(listFor1);
        Assert.Equal("for-1.pdf", listFor1[0].OriginalFileName);
        Assert.Equal(1, listFor1[0].TravelRequestId);
    }

    // ----------------------------------------------------------------
    // 5. Download/get a valid Travel document
    // ----------------------------------------------------------------
    [Fact]
    public async Task Download_ValidTravelDocument_ReturnsContent()
    {
        var db = CreateContext();
        AddTravel(db, 1, 100, "TRV-2026-000001");
        var svc = MakeService(db, MakeEnv());
        var content = new byte[] { 9, 8, 7, 6 };
        var uploaded = await svc.UploadAsync(1, CreateFormFile(content, "doc.pdf", "application/pdf"), null);

        var (bytes, contentType, fileName) = await svc.DownloadAsync(uploaded.Id);

        Assert.Equal(content, bytes);
        Assert.Equal("application/pdf", contentType);
        Assert.Equal("doc.pdf", fileName);
    }

    // ----------------------------------------------------------------
    // 6. Reject an attachment belonging to another module
    // ----------------------------------------------------------------
    [Fact]
    public async Task Download_AttachmentFromAnotherModule_ReturnsNotFound()
    {
        var db = CreateContext();
        var foreign = new Attachment
        {
            RelatedModule = "Meeting",
            RelatedEntity = "Meeting",
            RelatedEntityId = "50",
            OriginalFileName = "meeting-notes.pdf",
            ObjectKey = "Content/Meeting/50/x.pdf",
            IsActive = true,
            IsDeleted = false,
            UploadedBy = "someone",
            UploadedAt = DateTime.UtcNow,
            CreatedBy = "someone",
            CreatedDate = DateTime.UtcNow
        };
        db.Set<Attachment>().Add(foreign);
        await db.SaveChangesAsync();

        var svc = MakeService(db, MakeEnv());

        await Assert.ThrowsAsync<Jarvis5.Common.NotFoundException>(() => svc.DownloadAsync(foreign.Id));
        await Assert.ThrowsAsync<Jarvis5.Common.NotFoundException>(() => svc.DeleteAsync(foreign.Id));
    }

    // ----------------------------------------------------------------
    // 7 & 8. Soft delete / removed document no longer returned as active
    // ----------------------------------------------------------------
    [Fact]
    public async Task Delete_SoftDeletes_PreservesRowAndFile_AndIsExcludedFromList()
    {
        var db = CreateContext();
        AddTravel(db, 1, 100, "TRV-2026-000001");
        var env = MakeEnv();
        var svc = MakeService(db, env);
        var uploaded = await svc.UploadAsync(1, CreateFormFile(new byte[] { 1 }, "to-delete.pdf", "application/pdf"), null);

        await svc.DeleteAsync(uploaded.Id);

        var att = db.Set<Attachment>().AsNoTracking().Single(a => a.Id == uploaded.Id);
        Assert.True(att.IsDeleted);
        Assert.False(att.IsActive);
        // Row and physical file are preserved, not destroyed.
        var absolute = Path.GetFullPath(Path.Combine(env.ContentRootPath, att.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
        Assert.True(File.Exists(absolute));

        var list = await svc.ListAsync(1);
        Assert.Empty(list);

        // Deleted documents can no longer be downloaded via the Travel endpoint.
        await Assert.ThrowsAsync<Jarvis5.Common.NotFoundException>(() => svc.DownloadAsync(uploaded.Id));
    }

    // ----------------------------------------------------------------
    // 10. Same filename does not overwrite previous history
    // ----------------------------------------------------------------
    [Fact]
    public async Task Upload_SameFilenameTwice_CreatesSeparateRows_DoesNotOverwrite()
    {
        var db = CreateContext();
        AddTravel(db, 1, 100, "TRV-2026-000001");
        var svc = MakeService(db, MakeEnv());

        var first = await svc.UploadAsync(1, CreateFormFile(new byte[] { 1 }, "quotation.pdf", "application/pdf"), "Quotation");
        var second = await svc.UploadAsync(1, CreateFormFile(new byte[] { 2, 2 }, "quotation.pdf", "application/pdf"), "Quotation");

        Assert.NotEqual(first.Id, second.Id);
        var list = await svc.ListAsync(1);
        Assert.Equal(2, list.Count);
        Assert.All(list, d => Assert.Equal("quotation.pdf", d.OriginalFileName));

        var (firstBytes, _, _) = await svc.DownloadAsync(first.Id);
        var (secondBytes, _, _) = await svc.DownloadAsync(second.Id);
        Assert.Single(firstBytes);
        Assert.Equal(2, secondBytes.Length);
    }

    // ----------------------------------------------------------------
    // 11. Rework/new-cycle upload preserves the old document
    // ----------------------------------------------------------------
    [Fact]
    public async Task Upload_DuringReworkCycle_PreservesOlderCycleDocument()
    {
        var db = CreateContext();
        var travel = AddTravel(db, 1, 100, "TRV-2026-000001", "Draft", "Pending", currentCycleNo: 1);
        var svc = MakeService(db, MakeEnv());

        var cycle1Doc = await svc.UploadAsync(1, CreateFormFile(new byte[] { 1 }, "v1.pdf", "application/pdf"), null);
        Assert.Equal(1, cycle1Doc.CycleNo);

        // Simulate the resubmit lifecycle advancing CurrentCycleNo (already proven by
        // TravelRequestService.Lifecycle tests) without re-running that flow here.
        travel.CurrentCycleNo = 2;
        await db.SaveChangesAsync();

        var cycle2Doc = await svc.UploadAsync(1, CreateFormFile(new byte[] { 2 }, "v2.pdf", "application/pdf"), null);
        Assert.Equal(2, cycle2Doc.CycleNo);

        var list = await svc.ListAsync(1);
        Assert.Equal(2, list.Count);
        Assert.Contains(list, d => d.Id == cycle1Doc.Id && d.CycleNo == 1);
        Assert.Contains(list, d => d.Id == cycle2Doc.Id && d.CycleNo == 2);

        // The cycle-1 document is still fully retrievable — never overwritten or deleted.
        var (bytes, _, _) = await svc.DownloadAsync(cycle1Doc.Id);
        Assert.Single(bytes);
    }

    // ----------------------------------------------------------------
    // 12–17. Identity preserved; upload creates no side effects on
    // BusinessState/ApprovalState/EaTask/WorkflowInstance/TravelRequestCycle.
    // ----------------------------------------------------------------
    [Fact]
    public async Task Upload_DoesNotAffectTravelIdentityOrRelatedEntities()
    {
        var db = CreateContext();
        var travel = AddTravel(db, 1, 100, "TRV-2026-000001", "Draft", "NotSubmitted", currentCycleNo: 0);
        var svc = MakeService(db, MakeEnv());

        var tasksBefore = db.Tasks.Count();
        var workflowsBefore = db.WorkflowInstances.Count();
        var cyclesBefore = db.TravelRequestCycles.Count();

        await svc.UploadAsync(1, CreateFormFile(new byte[] { 1 }, "doc.pdf", "application/pdf"), "Other");

        var reloaded = await db.TravelRequests.AsNoTracking().SingleAsync(t => t.Id == 1);
        Assert.Equal("TRV-2026-000001", reloaded.ReferenceNo);
        Assert.Equal(100, reloaded.EaTaskId);
        Assert.Equal("Draft", reloaded.BusinessState);
        Assert.Equal("NotSubmitted", reloaded.ApprovalState);
        Assert.Equal(0, reloaded.CurrentCycleNo);

        Assert.Equal(tasksBefore, db.Tasks.Count());
        Assert.Equal(workflowsBefore, db.WorkflowInstances.Count());
        Assert.Equal(cyclesBefore, db.TravelRequestCycles.Count());
    }

    [Theory]
    [InlineData("Meeting", "Meeting")]
    [InlineData("Approval", "ApprovalRequest")]
    [InlineData("Travel", "ApprovalRequest")]
    public async Task ForeignAttachment_IsExcludedFromAllTravelOperations(string module, string entity)
    {
        using var db = CreateContext();
        AddTravel(db, 1, 100, "TRV-1");
        var svc = MakeService(db, MakeEnv());
        var doc = await svc.UploadAsync(1, CreateFormFile(new byte[] { 1 }, "a.pdf", "application/pdf"), null);
        var row = await db.Attachments.FindAsync(doc.Id);
        row!.RelatedModule = module;
        row.RelatedEntity = entity;
        await db.SaveChangesAsync();
        Assert.Empty(await svc.ListAsync(1));
        await Assert.ThrowsAsync<Jarvis5.Common.NotFoundException>(() => svc.DownloadAsync(doc.Id));
        await Assert.ThrowsAsync<Jarvis5.Common.NotFoundException>(() => svc.DeleteAsync(doc.Id));
        Assert.False(row.IsDeleted);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("deleted-parent")]
    [InlineData("missing-parent")]
    [InlineData("invalid-parent")]
    public async Task UnavailableDocument_CannotBeDownloadedOrDeleted(string scenario)
    {
        using var db = CreateContext();
        var travel = AddTravel(db, 1, 100, "TRV-1");
        var svc = MakeService(db, MakeEnv());
        var doc = await svc.UploadAsync(1, CreateFormFile(new byte[] { 1 }, "a.pdf", "application/pdf"), null);
        var row = await db.Attachments.FindAsync(doc.Id);
        if (scenario == "inactive") row!.IsActive = false;
        if (scenario == "deleted-parent") travel.IsDeleted = true;
        if (scenario == "missing-parent") row!.RelatedEntityId = "999";
        if (scenario == "invalid-parent") row!.RelatedEntityId = "../1";
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<Jarvis5.Common.NotFoundException>(() => svc.DownloadAsync(doc.Id));
        await Assert.ThrowsAsync<Jarvis5.Common.NotFoundException>(() => svc.DeleteAsync(doc.Id));
        if (scenario == "deleted-parent")
        {
            await Assert.ThrowsAsync<Jarvis5.Common.NotFoundException>(() => svc.ListAsync(1));
            await Assert.ThrowsAsync<Jarvis5.Common.NotFoundException>(() => svc.UploadAsync(1,
                CreateFormFile(new byte[] { 1 }, "a.pdf", "application/pdf"), null));
        }
        else Assert.Empty(await svc.ListAsync(1));
    }

    [Fact]
    public async Task UploadAndDelete_PersistSharedAudit_WithoutChangingReworkState()
    {
        using var db = CreateContext();
        var travel = AddTravel(db, 1, 100, "TRV-1", "Draft", "ChangesRequested", 1);
        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "tester" && u.UserId == 1);
        var actorResolver = Mock.Of<IEaActorResolver>(r =>
            r.ResolveDisplayNameAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult("tester"));
        var svc = new TravelDocumentService(db, user, new AuditService(db, user), MakeEnv(), actorResolver);
        var doc = await svc.UploadAsync(1, CreateFormFile(new byte[] { 1 }, "a.pdf", "application/pdf"), "Other");
        Assert.Equal(1, doc.CycleNo);
        await svc.DeleteAsync(doc.Id);
        db.ChangeTracker.Clear();
        var current = await db.TravelRequests.SingleAsync();
        Assert.Equal(travel.ReferenceNo, current.ReferenceNo);
        Assert.Equal(100, current.EaTaskId);
        Assert.Equal("Draft", current.BusinessState);
        Assert.Equal("ChangesRequested", current.ApprovalState);
        Assert.Equal(1, current.CurrentCycleNo);
        Assert.Empty(db.Tasks);
        Assert.Empty(db.WorkflowInstances);
        Assert.Empty(db.TravelRequestCycles);
        Assert.Equal(new[] { "TRAVEL_DOCUMENT_UPLOAD", "TRAVEL_DOCUMENT_DELETE" },
            await db.AuditLogs.OrderBy(a => a.Id).Select(a => a.ActionType).ToArrayAsync());
    }

    [Theory]
    [InlineData("Invitation")]
    [InlineData("PassportTravelDocument")]
    [InlineData("Quotation")]
    [InlineData("HotelQuotation")]
    [InlineData("Itinerary")]
    [InlineData("SupportingApprovalDocument")]
    [InlineData("Other")]
    [InlineData(null)]
    public async Task Category_IsOptionalAndPersisted(string? category)
    {
        using var db = CreateContext();
        AddTravel(db, 1, 100, "TRV-1");
        var svc = MakeService(db, MakeEnv());
        await svc.UploadAsync(1, CreateFormFile(new byte[] { 1 }, "a.pdf", "application/pdf"), category);
        db.ChangeTracker.Clear();
        Assert.Equal(category, (await svc.ListAsync(1)).Single().DocumentCategory);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("large")]
    [InlineData("extension")]
    public async Task InvalidFile_LeavesNoMetadataOrFile(string scenario)
    {
        using var db = CreateContext();
        AddTravel(db, 1, 100, "TRV-1");
        var env = MakeEnv();
        var svc = MakeService(db, env);
        var file = Mock.Of<IFormFile>(f => f.Length == (scenario == "empty" ? 0 : scenario == "large" ? 25 * 1024 * 1024 + 1 : 1)
            && f.FileName == (scenario == "extension" ? "a.exe" : "a.pdf"));
        await Assert.ThrowsAsync<Jarvis5.Common.BusinessRuleException>(() => svc.UploadAsync(1, file, null));
        Assert.Empty(db.Attachments);
        Assert.Empty(Directory.GetFiles(env.ContentRootPath, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task FilenameIsSanitized_StorageKeyIsNeverReturned()
    {
        using var db = CreateContext();
        AddTravel(db, 1, 100, "TRV-1");
        var svc = MakeService(db, MakeEnv());
        var dto = await svc.UploadAsync(1, CreateFormFile(new byte[] { 1 }, "../../private/a.pdf", "application/pdf"), null);
        Assert.Equal("a.pdf", dto.OriginalFileName);
        var row = await db.Attachments.SingleAsync();
        Assert.Null(row.AccessUrl);
        Assert.DoesNotContain(row.ObjectKey, JsonSerializer.Serialize(dto));
        row.ObjectKey = "Content/Approval/1/a.pdf";
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DownloadAsync(dto.Id));
    }
}
