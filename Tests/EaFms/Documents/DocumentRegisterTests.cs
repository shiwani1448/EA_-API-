using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Common;
using Jarvis5.Controllers.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.EaFms;
using Jarvis5.Tests.EaFms.Delegation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Documents;

/// <summary>
/// Central Document &amp; Records register + central download: a read model over ea_attachments. Runs the real service against a throwaway
/// PostgreSQL database (see ScratchTatDatabase), so nothing is written to the developer database and jsonb metadata is real.
/// </summary>
public class DocumentRegisterTests : IClassFixture<ScratchTatDatabase>, IAsyncLifetime, IDisposable
{
    private readonly ScratchTatDatabase _fx;
    private readonly string _parent = Path.Combine(Path.GetTempPath(), "docreg-" + Guid.NewGuid().ToString("N"));
    private string Root => Path.Combine(_parent, "app");      // ContentRootPath; files live under Root/Content, outside files under _parent
    private readonly Dictionary<string, long> _modules = new();
    private static readonly DateTime T0 = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    public DocumentRegisterTests(ScratchTatDatabase fx)
    {
        _fx = fx;
        Directory.CreateDirectory(Path.Combine(Root, "Content"));
    }

    public async Task InitializeAsync()
    {
        await using var db = _fx.Db();
        foreach (var name in new[] { "Meeting", "Delegation", "Travel & Hospitality", "EA Approval" })
        {
            var module = await db.BusinessModules.FirstOrDefaultAsync(m => m.Name == name);
            if (module is null)
            {
                module = new BusinessModule { Name = name, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
                db.BusinessModules.Add(module);
                await db.SaveChangesAsync();
            }
            _modules[name] = module.Id;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() { try { Directory.Delete(_parent, true); } catch { /* best-effort */ } }

    // ---------------- helpers ----------------
    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Count;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        { Interlocked.Increment(ref Count); return base.ReaderExecutingAsync(command, eventData, result, ct); }
    }

    private EaFmsDbContext Db(CommandCounter? counter = null)
    {
        var options = new DbContextOptionsBuilder<EaFmsDbContext>().UseNpgsql(_fx.Connection);
        if (counter is not null) options.AddInterceptors(counter);
        return new EaFmsDbContext(options.Options);
    }

    private DocumentRegisterService Service(EaFmsDbContext db) =>
        new(db, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(e => e.ContentRootPath == Root));

    private static string Unique(string p = "u") => $"{p}-{Guid.NewGuid():N}";

    private async Task<long> TaskAsync(string module, string recordId, string task, string? description = null)
    {
        await using var db = Db();
        var t = new EaTask { BusinessModuleId = _modules[module], ModuleName = module, BusinessRecordId = recordId, Task = task, Description = description,
            ExecutionStatus = "NotStarted", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.Tasks.Add(t);
        await db.SaveChangesAsync();
        return t.Id;
    }

    private async Task<long> AttachmentAsync(string relatedModule, string relatedEntity, string entityId, string uploader, DateTime? uploadedAt = null,
        string? metadata = "{}", string fileName = "file.pdf", bool active = true, bool deleted = false, string? objectKey = null, string contentType = "application/pdf", long size = 123)
    {
        await using var db = Db();
        var a = new Attachment
        {
            RelatedModule = relatedModule, RelatedEntity = relatedEntity, RelatedEntityId = entityId, OriginalFileName = fileName,
            ObjectKey = objectKey ?? $"Content/{relatedModule}/{entityId}/{Guid.NewGuid():N}.pdf", ContentType = contentType, Size = size,
            UploadedBy = uploader, UploadedAt = uploadedAt ?? DateTime.UtcNow, Metadata = metadata, IsActive = active, IsDeleted = deleted,
            CreatedBy = uploader, CreatedDate = DateTime.UtcNow
        };
        db.Attachments.Add(a);
        await db.SaveChangesAsync();
        return a.Id;
    }

    private async Task<PagedResult<DocumentRegisterRowDto>> ListAsync(DocumentRegisterQueryDto q)
    {
        await using var db = Db();
        return await Service(db).ListAsync(q, default);
    }

    private static bool Near(DateTime a, DateTime b) => Math.Abs((a - b).TotalMilliseconds) < 1;

    private string WriteFile(string relativeKey, string content)
    {
        var path = Path.Combine(Root, relativeKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return relativeKey;
    }

    // ============================================================
    // Source mapping
    // ============================================================
    [Fact]
    public async Task Meeting_Attachment_MapsToCanonicalModule_Task_Type_UploadTime_UploadedBy_AndDocument()
    {
        var uploader = Unique(); var record = Unique("m");
        var taskId = await TaskAsync("Meeting", record, "Board review meeting", "long description");
        var at = T0.AddHours(3);
        var id = await AttachmentAsync("Meeting", "Meeting", record, uploader, at, """{"purpose":"MeetingCompletionPdf"}""", "minutes.pdf", size: 4321);

        var row = Assert.Single((await ListAsync(new() { UploadedBy = uploader })).Items);

        Assert.Equal(id, row.AttachmentId);
        Assert.Equal(_modules["Meeting"], row.ModuleId);          // resolved from ea_business_modules by name
        Assert.Equal("Meeting", row.ModuleName);
        Assert.Equal("Completion PDF", row.Type);
        Assert.Equal(taskId, row.EaTaskId);
        Assert.Equal(record, row.BusinessRecordId);
        Assert.Equal("Board review meeting", row.TaskDescription);   // EaTask.Task, never EaTask.Description
        Assert.True(Near(at, row.UploadTime));
        Assert.Equal(uploader, row.UploadedBy);                    // Attachment.UploadedBy, as stored
        Assert.Equal((id, "minutes.pdf", "application/pdf", 4321L, $"/api/ea/documents/{id}/download"),
            (row.Document.AttachmentId, row.Document.FileName, row.Document.ContentType, row.Document.Size, row.Document.DownloadUrl));
    }

    [Fact]
    public async Task Delegation_Attachment_MapsAndIsACompletionPdf()
    {
        var uploader = Unique(); var record = "9" + Guid.NewGuid().ToString("N")[..6];
        var taskId = await TaskAsync("Delegation", record, "Prepare board deck");
        await AttachmentAsync("Delegation", "Delegation", record, uploader, T0, """{"purpose":"DelegationCompletionPdf","delegationId":1}""");

        var row = Assert.Single((await ListAsync(new() { UploadedBy = uploader })).Items);

        Assert.Equal((_modules["Delegation"], "Delegation", "Completion PDF", taskId, record, "Prepare board deck"),
            (row.ModuleId, row.ModuleName, row.Type, row.EaTaskId, row.BusinessRecordId, row.TaskDescription));
    }

    [Fact]
    public async Task Travel_Attachment_MapsToTheCanonicalTravelAndHospitalityModule_AndCategoryBecomesTheType()
    {
        var uploader = Unique(); var record = "4" + Guid.NewGuid().ToString("N")[..6];
        var taskId = await TaskAsync("Travel & Hospitality", record, "Delhi trip");
        await AttachmentAsync("Travel", "TravelRequest", record, uploader, T0, """{"DocumentCategory":"  Itinerary ","CycleNo":null}""");

        var row = Assert.Single((await ListAsync(new() { UploadedBy = uploader })).Items);

        Assert.Equal((_modules["Travel & Hospitality"], "Travel & Hospitality", "Itinerary", taskId, record, "Delhi trip"),
            (row.ModuleId, row.ModuleName, row.Type, row.EaTaskId, row.BusinessRecordId, row.TaskDescription));   // RelatedModule "Travel" is never the display name
    }

    [Theory]
    [InlineData("""{"DocumentCategory":null,"CycleNo":1}""")]
    [InlineData("""{"DocumentCategory":"   "}""")]
    [InlineData("""{}""")]
    [InlineData("""{"DocumentCategory":5}""")]
    [InlineData("""[1,2,3]""")]
    [InlineData("""null""")]
    public async Task Travel_MissingBlankOrOddCategory_FallsBackToTravelDocument(string metadata)
    {
        var uploader = Unique();
        await AttachmentAsync("Travel", "TravelRequest", "1", uploader, T0, metadata);

        Assert.Equal("Travel Document", Assert.Single((await ListAsync(new() { UploadedBy = uploader })).Items).Type);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("")]
    [InlineData(null)]
    public void MalformedOrMissingMetadata_NeverCrashesTheTypeMapping(string? metadata)
    {
        Assert.Equal("Travel Document", DocumentRegisterService.ResolveType("Travel", metadata));
        Assert.Equal("Document", DocumentRegisterService.ResolveType("Meeting", metadata));
        Assert.Equal("Document", DocumentRegisterService.ResolveType("Delegation", metadata));
        Assert.Equal("Approval Document", DocumentRegisterService.ResolveType("Approval", metadata));
    }

    [Fact]
    public async Task Approval_NumericRequestId_IsResolvedToTheReferenceNo_AndThenToTheRightEaTask()
    {
        var uploader = Unique(); var reference = "REF-" + Guid.NewGuid().ToString("N")[..8];
        var taskId = await TaskAsync("EA Approval", reference, "Approve laptop purchase");
        long requestId;
        await using (var db = Db())
        {
            var request = new ApprovalRequest { EaTaskId = taskId, ReferenceNo = reference, CreatedBy = "seed", CreatedAt = DateTime.UtcNow };
            db.ApprovalRequests.Add(request);
            await db.SaveChangesAsync();
            requestId = request.Id;
        }
        // a different task whose BusinessRecordId equals the numeric request id must NOT be picked
        await TaskAsync("EA Approval", requestId.ToString(), "Wrong task");
        await AttachmentAsync("Approval", "ApprovalRequest", requestId.ToString(), uploader, T0, """{"ApprovalCycleId":3}""");

        var row = Assert.Single((await ListAsync(new() { UploadedBy = uploader })).Items);

        Assert.Equal((_modules["EA Approval"], "EA Approval", "Approval Document", taskId, reference, "Approve laptop purchase"),
            (row.ModuleId, row.ModuleName, row.Type, row.EaTaskId, row.BusinessRecordId, row.TaskDescription));
    }

    [Fact]
    public async Task ARowWithoutAnEaTask_StillAppears_WithNullTaskFields()
    {
        var uploader = Unique();
        await AttachmentAsync("Meeting", "Meeting", Unique("nomatch"), uploader, T0, """{"purpose":"MeetingCompletionPdf"}""");

        var row = Assert.Single((await ListAsync(new() { UploadedBy = uploader })).Items);

        Assert.Null(row.EaTaskId);
        Assert.Null(row.TaskDescription);
        Assert.NotNull(row.BusinessRecordId);
    }

    [Fact]
    public async Task UploadedBy_IsDisplayedExactlyAsStored_EvenTheUnauthenticatedZero()
    {
        await AttachmentAsync("Meeting", "Meeting", "1", "0", T0.AddYears(-30), """{"purpose":"MeetingCompletionPdf"}""");

        var page = await ListAsync(new() { UploadedBy = "0", ToDate = T0.AddYears(-30) });

        Assert.Contains(page.Items, r => r.UploadedBy == "0");
    }

    // ============================================================
    // Inclusion rules
    // ============================================================
    [Fact]
    public async Task InactiveDeletedAndNonEaRows_AreExcluded()
    {
        var uploader = Unique();
        var visible = await AttachmentAsync("Meeting", "Meeting", "1", uploader, T0);
        await AttachmentAsync("Meeting", "Meeting", "2", uploader, T0, active: false);
        await AttachmentAsync("Meeting", "Meeting", "3", uploader, T0, deleted: true);
        await AttachmentAsync("Travel", "TravelRequest", "4", uploader, T0, active: false, deleted: true);
        await AttachmentAsync("Travel", "SomethingElse", "5", uploader, T0);    // unsupported RelatedEntity for that module
        await AttachmentAsync("Unknown", "Unknown", "6", uploader, T0);          // not an EA document producer

        var page = await ListAsync(new() { UploadedBy = uploader });

        Assert.Equal(visible, Assert.Single(page.Items).AttachmentId);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Ordering_IsUploadedAtDescending_ThenIdDescending()
    {
        var uploader = Unique();
        var older = await AttachmentAsync("Meeting", "Meeting", "1", uploader, T0);
        var newest = await AttachmentAsync("Delegation", "Delegation", "2", uploader, T0.AddHours(5));
        var tieA = await AttachmentAsync("Travel", "TravelRequest", "3", uploader, T0.AddHours(2));
        var tieB = await AttachmentAsync("Meeting", "Meeting", "4", uploader, T0.AddHours(2));

        var ids = (await ListAsync(new() { UploadedBy = uploader })).Items.Select(i => i.AttachmentId).ToList();

        Assert.Equal(new[] { newest, tieB, tieA, older }, ids);
    }

    // ============================================================
    // Paging and filters
    // ============================================================
    [Fact]
    public async Task Pagination_SplitsThePages_ReportsTotals_AndClampsPageSize()
    {
        var uploader = Unique();
        for (var i = 0; i < 5; i++) await AttachmentAsync("Meeting", "Meeting", i.ToString(), uploader, T0.AddMinutes(i));

        var p1 = await ListAsync(new() { UploadedBy = uploader, Page = 1, PageSize = 2 });
        var p2 = await ListAsync(new() { UploadedBy = uploader, Page = 2, PageSize = 2 });
        var p3 = await ListAsync(new() { UploadedBy = uploader, Page = 3, PageSize = 2 });
        var p4 = await ListAsync(new() { UploadedBy = uploader, Page = 4, PageSize = 2 });

        Assert.Equal((2, 2, 1, 0), (p1.Items.Count, p2.Items.Count, p3.Items.Count, p4.Items.Count));
        Assert.All(new[] { p1, p2, p3, p4 }, p => Assert.Equal((5, 3), (p.TotalCount, p.TotalPages)));
        Assert.Equal(5, p1.Items.Concat(p2.Items).Concat(p3.Items).Select(i => i.AttachmentId).Distinct().Count());

        var defaults = await ListAsync(new() { UploadedBy = uploader, Page = 0, PageSize = 0 });
        Assert.Equal((1, 50), (defaults.PageNumber, defaults.PageSize));
        Assert.Equal(200, (await ListAsync(new() { UploadedBy = uploader, PageSize = 100000 })).PageSize);
    }

    [Fact]
    public async Task BusinessModuleId_Filter_UsesTheCanonicalModuleId()
    {
        var uploader = Unique();
        await AttachmentAsync("Meeting", "Meeting", "1", uploader, T0);
        await AttachmentAsync("Travel", "TravelRequest", "2", uploader, T0);
        await AttachmentAsync("Approval", "ApprovalRequest", "3", uploader, T0);

        var travel = await ListAsync(new() { UploadedBy = uploader, BusinessModuleId = _modules["Travel & Hospitality"] });
        var none = await ListAsync(new() { UploadedBy = uploader, BusinessModuleId = 987654321 });

        Assert.Equal("Travel & Hospitality", Assert.Single(travel.Items).ModuleName);
        Assert.Empty(none.Items);
        await Assert.ThrowsAsync<BadRequestException>(() => ListAsync(new() { BusinessModuleId = 0 }));
    }

    [Fact]
    public async Task Type_Filter_MatchesTheNormalizedTypeCaseInsensitively_AndPaginatesTheFilteredSet()
    {
        var uploader = Unique();
        await AttachmentAsync("Meeting", "Meeting", "1", uploader, T0, """{"purpose":"MeetingCompletionPdf"}""");
        await AttachmentAsync("Delegation", "Delegation", "2", uploader, T0, """{"purpose":"DelegationCompletionPdf"}""");
        await AttachmentAsync("Travel", "TravelRequest", "3", uploader, T0, """{"DocumentCategory":"Itinerary"}""");
        await AttachmentAsync("Travel", "TravelRequest", "4", uploader, T0, """{}""");
        await AttachmentAsync("Approval", "ApprovalRequest", "5", uploader, T0);

        var completion = await ListAsync(new() { UploadedBy = uploader, Type = "completion pdf", PageSize = 1 });

        Assert.Equal(2, completion.TotalCount);
        Assert.Single(completion.Items);
        Assert.Equal(1, (await ListAsync(new() { UploadedBy = uploader, Type = "ITINERARY" })).TotalCount);
        Assert.Equal(1, (await ListAsync(new() { UploadedBy = uploader, Type = "Travel Document" })).TotalCount);
        Assert.Equal(1, (await ListAsync(new() { UploadedBy = uploader, Type = "Approval Document" })).TotalCount);
        Assert.Equal(0, (await ListAsync(new() { UploadedBy = uploader, Type = "Nothing" })).TotalCount);
    }

    [Fact]
    public async Task UploadedBy_Filter_IsCaseInsensitiveExactMatch()
    {
        var mine = "Person-" + Guid.NewGuid().ToString("N")[..8];
        await AttachmentAsync("Meeting", "Meeting", "1", mine, T0);
        await AttachmentAsync("Meeting", "Meeting", "2", mine + "x", T0);

        var page = await ListAsync(new() { UploadedBy = "  " + mine.ToUpperInvariant() + " " });

        Assert.Equal(mine, Assert.Single(page.Items).UploadedBy);
    }

    [Fact]
    public async Task DateFilters_UseIndiaCalendarDayBoundaries_Inclusively()
    {
        var uploader = Unique();
        // India day 2026-03-10 runs from 2026-03-09T18:30Z to 2026-03-10T18:30Z
        var beforeDay = await AttachmentAsync("Meeting", "Meeting", "1", uploader, new DateTime(2026, 3, 9, 18, 29, 0, DateTimeKind.Utc));
        var dayStart = await AttachmentAsync("Meeting", "Meeting", "2", uploader, new DateTime(2026, 3, 9, 18, 30, 0, DateTimeKind.Utc));
        var dayEnd = await AttachmentAsync("Meeting", "Meeting", "3", uploader, new DateTime(2026, 3, 10, 18, 29, 0, DateTimeKind.Utc));
        var nextDay = await AttachmentAsync("Meeting", "Meeting", "4", uploader, new DateTime(2026, 3, 10, 18, 30, 0, DateTimeKind.Utc));
        var day = new DateTime(2026, 3, 10);

        var only = (await ListAsync(new() { UploadedBy = uploader, FromDate = day, ToDate = day })).Items.Select(i => i.AttachmentId).ToHashSet();
        var from = (await ListAsync(new() { UploadedBy = uploader, FromDate = day })).Items.Select(i => i.AttachmentId).ToHashSet();
        var to = (await ListAsync(new() { UploadedBy = uploader, ToDate = day })).Items.Select(i => i.AttachmentId).ToHashSet();

        Assert.Equal(new[] { dayStart, dayEnd }.ToHashSet(), only);
        Assert.Equal(new[] { dayStart, dayEnd, nextDay }.ToHashSet(), from);
        Assert.Equal(new[] { beforeDay, dayStart, dayEnd }.ToHashSet(), to);
        await Assert.ThrowsAsync<BadRequestException>(() => ListAsync(new() { FromDate = day.AddDays(1), ToDate = day }));
    }

    [Fact]
    public async Task Search_MatchesFileName_TaskText_AndModuleName_CaseInsensitively()
    {
        var uploader = Unique(); var token = "Zq" + Guid.NewGuid().ToString("N")[..8];
        var byName = await AttachmentAsync("Meeting", "Meeting", "1", uploader, T0, fileName: $"Annual-{token}-report.pdf");
        var record = Unique("d");
        await TaskAsync("Delegation", record, $"Finalise {token} vendor contract");
        var byTask = await AttachmentAsync("Delegation", "Delegation", record, uploader, T0.AddMinutes(1));
        await AttachmentAsync("Meeting", "Meeting", "3", uploader, T0.AddMinutes(2), fileName: "unrelated.pdf");

        var hits = (await ListAsync(new() { UploadedBy = uploader, Search = "  " + token.ToUpperInvariant() + " " })).Items.Select(i => i.AttachmentId).ToList();
        var byModule = await ListAsync(new() { UploadedBy = uploader, Search = "delegation" });
        var page = await ListAsync(new() { UploadedBy = uploader, Search = token, PageSize = 1 });

        Assert.Equal(new[] { byTask, byName }, hits);      // newest first
        Assert.Equal(byTask, Assert.Single(byModule.Items).AttachmentId);
        Assert.Equal((2, 1), (page.TotalCount, page.Items.Count));
    }

    // ============================================================
    // Central download
    // ============================================================
    private async Task<DocumentDownload> DownloadAsync(long id)
    {
        await using var db = Db();
        return await Service(db).DownloadAsync(id, default);
    }

    [Fact]
    public async Task Download_ReturnsTheFile_WithTheStoredContentTypeAndOriginalFileName()
    {
        var key = WriteFile($"Content/DelegationCompletion/1/{Guid.NewGuid():N}.pdf", "%PDF-fake-content");
        var id = await AttachmentAsync("Delegation", "Delegation", "1", Unique(), objectKey: key, fileName: "Completion Report.pdf", contentType: "application/pdf");

        var file = await DownloadAsync(id);
        await using (file.Content)
        {
            using var reader = new StreamReader(file.Content);
            Assert.Equal("%PDF-fake-content", await reader.ReadToEndAsync());
        }

        Assert.Equal(("application/pdf", "Completion Report.pdf"), (file.ContentType, file.FileName));
    }

    [Fact]
    public async Task Download_WorksForEveryProducer_AndFallsBackToOctetStreamWithoutAContentType()
    {
        foreach (var (module, entity) in new[] { ("Meeting", "Meeting"), ("Delegation", "Delegation"), ("Travel", "TravelRequest"), ("Approval", "ApprovalRequest") })
        {
            var key = WriteFile($"Content/{module}/{Guid.NewGuid():N}.bin", "x");
            var id = await AttachmentAsync(module, entity, "1", Unique(), objectKey: key, contentType: "");
            var file = await DownloadAsync(id);
            await file.Content.DisposeAsync();
            Assert.Equal("application/octet-stream", file.ContentType);
        }
    }

    [Fact]
    public async Task Download_MissingAttachment_InactiveDeletedOrNonEa_AreNotFound()
    {
        var key = WriteFile($"Content/Meeting/{Guid.NewGuid():N}.pdf", "x");
        var inactive = await AttachmentAsync("Meeting", "Meeting", "1", Unique(), objectKey: key, active: false);
        var deleted = await AttachmentAsync("Meeting", "Meeting", "1", Unique(), objectKey: key, deleted: true);
        var nonEa = await AttachmentAsync("Unknown", "Unknown", "1", Unique(), objectKey: key);
        var wrongEntity = await AttachmentAsync("Travel", "Other", "1", Unique(), objectKey: key);

        foreach (var id in new[] { 987654321L, inactive, deleted, nonEa, wrongEntity })
            await Assert.ThrowsAsync<NotFoundException>(() => DownloadAsync(id));
    }

    [Fact]
    public async Task Download_MissingPhysicalFile_IsNotFound()
    {
        var id = await AttachmentAsync("Meeting", "Meeting", "1", Unique(), objectKey: $"Content/MeetingCompletion/{Guid.NewGuid():N}-does-not-exist.pdf");

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => DownloadAsync(id));

        Assert.Equal("Document file is not available.", ex.Message);
    }

    [Fact]
    public async Task Download_PathTraversalAndEscapedKeys_AreRejected_AndNothingOutsideContentIsRead()
    {
        File.WriteAllText(Path.Combine(_parent, "secret.txt"), "TOP-SECRET");                    // outside the content root
        File.WriteAllText(Path.Combine(Root, "appsettings.txt"), "APP-SECRET");                 // inside the app but outside /Content
        Directory.CreateDirectory(Path.Combine(Root, "Content2"));
        File.WriteAllText(Path.Combine(Root, "Content2", "sibling.txt"), "SIBLING");            // sibling folder sharing the "Content" prefix
        var keys = new[]
        {
            "../secret.txt", "Content/../../secret.txt", "Content/../appsettings.txt", "Content/..\\..\\secret.txt",
            Path.Combine(_parent, "secret.txt"),          // rooted absolute path
            "Content2/sibling.txt", "appsettings.txt", "/etc/passwd", "C:\\Windows\\win.ini", "\\\\server\\share\\file.pdf"
        };

        foreach (var key in keys)
        {
            var id = await AttachmentAsync("Meeting", "Meeting", "1", Unique(), objectKey: key);
            await Assert.ThrowsAsync<NotFoundException>(() => DownloadAsync(id));
        }
    }

    // ============================================================
    // Contract, safety, performance
    // ============================================================
    [Fact]
    public async Task ObjectKeyAndPhysicalPaths_AreNeverExposed()
    {
        var uploader = Unique(); var key = $"Content/MeetingCompletion/77/{Guid.NewGuid():N}.pdf";
        await AttachmentAsync("Meeting", "Meeting", "1", uploader, T0, objectKey: key);

        var json = JsonSerializer.Serialize(await ListAsync(new() { UploadedBy = uploader }));

        Assert.DoesNotContain(key, json);
        Assert.DoesNotContain("Content/", json);
        Assert.DoesNotContain("objectKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Root, json);
        foreach (var t in new[] { typeof(DocumentRegisterRowDto), typeof(DocumentFileDto), typeof(DocumentRegisterQueryDto) })
            Assert.DoesNotContain(t.GetProperties(), p => p.Name.Contains("ObjectKey") || p.Name.Contains("Path") || p.Name.Contains("AccessUrl"));
    }

    [Fact]
    public async Task ListAndDownload_WriteNothing()
    {
        var uploader = Unique();
        var key = WriteFile($"Content/Meeting/{Guid.NewGuid():N}.pdf", "x");
        var id = await AttachmentAsync("Meeting", "Meeting", "1", uploader, T0, objectKey: key);
        async Task<(int, int, int, int)> Counts() { await using var db = Db(); return (await db.Attachments.CountAsync(), await db.Tasks.CountAsync(), await db.AuditLogs.CountAsync(), await db.ApprovalRequests.CountAsync()); }
        var before = await Counts();

        await using var db2 = Db();
        var service = Service(db2);
        await service.ListAsync(new() { UploadedBy = uploader }, default);
        await service.ListAsync(new() { Search = "x", Type = "Completion PDF" }, default);
        await (await service.DownloadAsync(id, default)).Content.DisposeAsync();

        Assert.False(db2.ChangeTracker.HasChanges());
        Assert.Empty(db2.ChangeTracker.Entries());     // everything was read AsNoTracking
        Assert.Equal(before, await Counts());
    }

    [Fact]
    public void NoHrmsUsersOrJwtDependency_TheServiceOnlyNeedsTheEaContextAndTheHostEnvironment()
    {
        var parameters = typeof(DocumentRegisterService).GetConstructors().Single().GetParameters().Select(p => p.ParameterType).ToArray();

        Assert.Equal(new[] { typeof(EaFmsDbContext), typeof(Microsoft.AspNetCore.Hosting.IWebHostEnvironment) }, parameters);
        Assert.DoesNotContain(typeof(DocumentRegisterService).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            f => f.FieldType.FullName!.Contains("hrms", StringComparison.OrdinalIgnoreCase) || f.FieldType.Name.Contains("Actor") || f.FieldType.Name.Contains("CurrentUser"));
    }

    [Fact]
    public async Task NoNPlusOne_TheQueryCountIsTheSameForThreeRowsAndForAHundredRows()
    {
        async Task<int> CountQueriesAsync(string uploader, Func<DocumentRegisterQueryDto> query)
        {
            var counter = new CommandCounter();
            await using var db = Db(counter);
            var page = await Service(db).ListAsync(query(), default);
            Assert.NotEmpty(page.Items);
            return counter.Count;
        }

        var small = Unique();
        for (var i = 0; i < 3; i++) await AttachmentAsync("Meeting", "Meeting", $"s{i}", small, T0.AddMinutes(i));
        var large = Unique();
        for (var i = 0; i < 100; i++)
        {
            var (module, entity) = (i % 3) switch { 0 => ("Meeting", "Meeting"), 1 => ("Delegation", "Delegation"), _ => ("Travel", "TravelRequest") };
            await AttachmentAsync(module, entity, $"l{i}", large, T0.AddMinutes(i));
        }

        var smallCount = await CountQueriesAsync(small, () => new() { UploadedBy = small, PageSize = 200 });
        var largePage = await CountQueriesAsync(large, () => new() { UploadedBy = large, PageSize = 200 });
        var largeSearch = await CountQueriesAsync(large, () => new() { UploadedBy = large, PageSize = 200, Search = "file" });

        Assert.InRange(largePage, 1, 9);                        // modules + count + page + one task query per module (+ approvals): a constant
        Assert.InRange(largeSearch, 1, 9);
        Assert.InRange(smallCount, 1, 9);
        Assert.True(largePage <= smallCount + 2, $"query count grew with rows: {smallCount} -> {largePage}");
    }

    [Fact]
    public void Routes_AndContract_AreAsSpecified()
    {
        var list = typeof(DocumentsController).GetMethod(nameof(DocumentsController.List))!;
        var download = typeof(DocumentsController).GetMethod(nameof(DocumentsController.Download))!;

        Assert.Equal("api/ea/documents", typeof(DocumentsController).GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.NotNull(list.GetCustomAttribute<HttpGetAttribute>());
        Assert.Equal("{attachmentId:long}/download", download.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Contains(list.GetCustomAttributes<ProducesResponseTypeAttribute>(), a => a.Type == typeof(PagedResult<DocumentRegisterRowDto>) && a.StatusCode == 200);
        Assert.Equal(new[] { "AttachmentId", "ModuleId", "ModuleName", "Type", "EaTaskId", "BusinessRecordId", "TaskDescription", "UploadTime", "UploadedBy", "Document" },
            typeof(DocumentRegisterRowDto).GetProperties().Select(p => p.Name));
        Assert.Equal(new[] { "AttachmentId", "FileName", "ContentType", "Size", "DownloadUrl" }, typeof(DocumentFileDto).GetProperties().Select(p => p.Name));
        Assert.Equal(new[] { "BusinessModuleId", "Type", "UploadedBy", "FromDate", "ToDate", "Search", "Page", "PageSize" },
            typeof(DocumentRegisterQueryDto).GetProperties().Select(p => p.Name));
    }
}
