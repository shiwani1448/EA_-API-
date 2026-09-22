using System.Globalization;
using System.Text.Json;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Jarvis5.Services.EaFms;

public sealed record DocumentDownload(Stream Content, string ContentType, string FileName);

public interface IDocumentRegisterService
{
    Task<PagedResult<DocumentRegisterRowDto>> ListAsync(DocumentRegisterQueryDto query, CancellationToken ct = default);
    Task<DocumentDownload> DownloadAsync(long attachmentId, CancellationToken ct = default);
}

/// <summary>
/// Central Document &amp; Records: a READ MODEL over the existing ea_attachments rows written by the Meeting, Delegation,
/// Travel and Approval upload flows. It creates no records, copies no files and holds no document table of its own; the source
/// modules do not know it exists. Nothing here depends on HRMS, Users or JWT — the uploader is the stored Attachment.UploadedBy.
/// </summary>
public class DocumentRegisterService(EaFmsDbContext db, IWebHostEnvironment env) : IDocumentRegisterService
{
    // Attachment (RelatedModule, RelatedEntity) written by each producer -> canonical BusinessModule NAME.
    // Module ids are always resolved from ea_business_modules by name; no numeric id is hardcoded.
    private sealed record Source(string RelatedModule, string RelatedEntity, string CanonicalModuleName);

    private static readonly Source[] Sources =
    [
        new("Meeting", "Meeting", "Meeting"),
        new("Delegation", "Delegation", "Delegation"),
        new("Travel", "TravelRequest", "Travel & Hospitality"),
        new("Approval", "ApprovalRequest", "EA Approval")
    ];

    public const string CompletionPdfType = "Completion PDF";
    public const string TravelDocumentType = "Travel Document";
    public const string ApprovalDocumentType = "Approval Document";
    public const string GenericDocumentType = "Document";
    private const int DefaultPageSize = 50, MaxPageSize = 200;

    private sealed record ResolvedSource(Source Source, BusinessModule Module);

    private sealed record AttachmentRow(long Id, string? RelatedModule, string? RelatedEntityId, string OriginalFileName,
        string? ContentType, long Size, string UploadedBy, DateTime UploadedAt, string? Metadata);

    // ============================================================
    // LIST
    // ============================================================

    public async Task<PagedResult<DocumentRegisterRowDto>> ListAsync(DocumentRegisterQueryDto query, CancellationToken ct = default)
    {
        if (query.BusinessModuleId.HasValue && query.BusinessModuleId.Value <= 0)
            throw new BadRequestException("BusinessModuleId must be positive.");
        if (query.FromDate.HasValue && query.ToDate.HasValue && query.FromDate.Value.Date > query.ToDate.Value.Date)
            throw new BadRequestException("FromDate must be on or before ToDate.");
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? DefaultPageSize : query.PageSize > MaxPageSize ? MaxPageSize : query.PageSize;

        var resolved = await ResolveSourcesAsync(ct);
        if (query.BusinessModuleId.HasValue)
            resolved = resolved.Where(r => r.Module.Id == query.BusinessModuleId.Value).ToList();
        if (resolved.Count == 0) return Empty(page, pageSize);

        var relatedModules = resolved.Select(r => r.Source.RelatedModule).ToList();
        var pairs = resolved.Select(r => r.Source.RelatedModule + "|" + r.Source.RelatedEntity).ToList();
        var attachments = db.Attachments.AsNoTracking()
            .Where(a => a.IsActive && !a.IsDeleted && a.RelatedModule != null && relatedModules.Contains(a.RelatedModule)
                && pairs.Contains(a.RelatedModule + "|" + a.RelatedEntity));

        if (!string.IsNullOrWhiteSpace(query.UploadedBy))
        {
            var uploader = query.UploadedBy.Trim().ToLower();
            attachments = attachments.Where(a => a.UploadedBy.ToLower() == uploader);
        }
        if (query.FromDate.HasValue)
        {
            var from = IndiaDateStartUtc(query.FromDate.Value);
            attachments = attachments.Where(a => a.UploadedAt >= from);
        }
        if (query.ToDate.HasValue)
        {
            var until = IndiaDateStartUtc(query.ToDate.Value.Date.AddDays(1));
            attachments = attachments.Where(a => a.UploadedAt < until);
        }

        var ordered = attachments.OrderByDescending(a => a.UploadedAt).ThenByDescending(a => a.Id);
        var typeFilter = string.IsNullOrWhiteSpace(query.Type) ? null : query.Type.Trim();
        var searchTerm = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim().ToLowerInvariant();

        // type is a derived (read-model) value and search covers the task text, so those two filters are applied after the batched
        // enrichment. Without them the page itself is cut in the database and only the page is enriched.
        if (typeFilter is null && searchTerm is null)
        {
            var total = await attachments.CountAsync(ct);
            var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(Projection).ToListAsync(ct);
            var items = await EnrichAsync(rows, resolved, ct);
            return new PagedResult<DocumentRegisterRowDto> { Items = items, PageNumber = page, PageSize = pageSize, TotalCount = total };
        }

        var candidates = await ordered.Select(Projection).ToListAsync(ct);
        var enriched = await EnrichAsync(candidates, resolved, ct);
        IEnumerable<DocumentRegisterRowDto> filtered = enriched;
        if (typeFilter is not null)
            filtered = filtered.Where(d => string.Equals(d.Type, typeFilter, StringComparison.OrdinalIgnoreCase));
        if (searchTerm is not null)
            filtered = filtered.Where(d => Contains(d.Document.FileName, searchTerm) || Contains(d.TaskDescription, searchTerm)
                || Contains(d.ModuleName, searchTerm) || Contains(d.Type, searchTerm));
        var matched = filtered.ToList();   // already newest-first: enrichment preserves the query order
        return new PagedResult<DocumentRegisterRowDto>
        {
            Items = matched.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            PageNumber = page, PageSize = pageSize, TotalCount = matched.Count
        };
    }

    private static readonly System.Linq.Expressions.Expression<Func<Attachment, AttachmentRow>> Projection = a =>
        new AttachmentRow(a.Id, a.RelatedModule, a.RelatedEntityId, a.OriginalFileName, a.ContentType, a.Size, a.UploadedBy, a.UploadedAt, a.Metadata);

    private static PagedResult<DocumentRegisterRowDto> Empty(int page, int pageSize) =>
        new() { Items = [], PageNumber = page, PageSize = pageSize, TotalCount = 0 };

    private static bool Contains(string? value, string lowerTerm) =>
        value is not null && value.Contains(lowerTerm, StringComparison.OrdinalIgnoreCase);

    // Same India calendar-day boundary the EM Report uses for its date filters.
    private static DateTime IndiaDateStartUtc(DateTime date) => DateTime.SpecifyKind(date.Date.AddHours(-5.5), DateTimeKind.Utc);

    /// <summary>The canonical BusinessModule of every producer, resolved by name (a missing/inactive module simply contributes no rows).</summary>
    private async Task<List<ResolvedSource>> ResolveSourcesAsync(CancellationToken ct)
    {
        var modules = await db.BusinessModules.AsNoTracking().Where(m => m.IsActive && !m.IsDeleted).OrderBy(m => m.Id).ToListAsync(ct);
        var resolved = new List<ResolvedSource>();
        foreach (var source in Sources)
        {
            var module = modules.FirstOrDefault(m => string.Equals(m.Name?.Trim(), source.CanonicalModuleName, StringComparison.OrdinalIgnoreCase));
            if (module is not null) resolved.Add(new ResolvedSource(source, module));
        }
        return resolved;
    }

    // ============================================================
    // ENRICHMENT — one batched task query per module (plus one approval-request query); never per row
    // ============================================================

    private async Task<List<DocumentRegisterRowDto>> EnrichAsync(List<AttachmentRow> rows, List<ResolvedSource> resolved, CancellationToken ct)
    {
        if (rows.Count == 0) return [];

        // Approval attachments carry the numeric ApprovalRequest.Id; the EaTask identity is its ReferenceNo.
        var referenceByApprovalId = new Dictionary<long, string>();
        var approvalIds = rows.Where(r => r.RelatedModule == "Approval")
            .Select(r => long.TryParse(r.RelatedEntityId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? (long?)id : null)
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (approvalIds.Count > 0)
            referenceByApprovalId = (await db.ApprovalRequests.AsNoTracking().Where(r => approvalIds.Contains(r.Id))
                .Select(r => new { r.Id, r.ReferenceNo }).ToListAsync(ct)).ToDictionary(r => r.Id, r => r.ReferenceNo);

        string? BusinessRecordIdOf(AttachmentRow row) => row.RelatedModule == "Approval"
            ? long.TryParse(row.RelatedEntityId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && referenceByApprovalId.TryGetValue(id, out var reference) ? reference : null
            : row.RelatedEntityId;

        var tasksByModule = new Dictionary<long, Dictionary<string, (long Id, string Task)>>();
        foreach (var source in resolved)
        {
            var recordIds = rows.Where(r => r.RelatedModule == source.Source.RelatedModule).Select(BusinessRecordIdOf)
                .Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).Distinct().ToList();
            if (recordIds.Count == 0) continue;
            var moduleId = source.Module.Id;
            var tasks = await db.Tasks.AsNoTracking()
                .Where(t => !t.IsDeleted && t.BusinessModuleId == moduleId && recordIds.Contains(t.BusinessRecordId))
                .OrderBy(t => t.Id).Select(t => new { t.Id, t.BusinessRecordId, t.Task }).ToListAsync(ct);
            var byRecord = new Dictionary<string, (long, string)>(StringComparer.Ordinal);
            foreach (var t in tasks) byRecord.TryAdd(t.BusinessRecordId, (t.Id, t.Task));
            tasksByModule[moduleId] = byRecord;
        }

        var moduleBySource = resolved.ToDictionary(r => r.Source.RelatedModule);
        var result = new List<DocumentRegisterRowDto>(rows.Count);
        foreach (var row in rows)
        {
            if (row.RelatedModule is null || !moduleBySource.TryGetValue(row.RelatedModule, out var source)) continue;
            var recordId = BusinessRecordIdOf(row);
            (long Id, string Task)? task = recordId is not null && tasksByModule.TryGetValue(source.Module.Id, out var map) && map.TryGetValue(recordId, out var found) ? found : null;
            result.Add(new DocumentRegisterRowDto
            {
                AttachmentId = row.Id,
                ModuleId = source.Module.Id,
                ModuleName = source.Module.Name,
                Type = ResolveType(row.RelatedModule, row.Metadata),
                EaTaskId = task?.Id,
                BusinessRecordId = recordId,
                TaskDescription = task?.Task,
                UploadTime = row.UploadedAt,
                UploadedBy = row.UploadedBy,
                Document = new DocumentFileDto
                {
                    AttachmentId = row.Id, FileName = row.OriginalFileName, ContentType = row.ContentType, Size = row.Size,
                    DownloadUrl = $"/api/ea/documents/{row.Id.ToString(CultureInfo.InvariantCulture)}/download"
                }
            });
        }
        return result;
    }

    // ============================================================
    // TYPE (read-model mapping only; no column, no migration)
    // ============================================================

    internal static string ResolveType(string? relatedModule, string? metadata)
    {
        switch (relatedModule)
        {
            case "Meeting":
                return string.Equals(ReadString(metadata, "purpose"), "MeetingCompletionPdf", StringComparison.OrdinalIgnoreCase) ? CompletionPdfType : GenericDocumentType;
            case "Delegation":
                return string.Equals(ReadString(metadata, "purpose"), "DelegationCompletionPdf", StringComparison.OrdinalIgnoreCase) ? CompletionPdfType : GenericDocumentType;
            case "Travel":
                var category = ReadString(metadata, "DocumentCategory")?.Trim();
                return string.IsNullOrEmpty(category) ? TravelDocumentType : category;
            case "Approval":
                return ApprovalDocumentType;
            default:
                return GenericDocumentType;
        }
    }

    /// <summary>Malformed, missing or non-object metadata yields null instead of failing the register.</summary>
    private static string? ReadString(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            foreach (var property in doc.RootElement.EnumerateObject())
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
                    return property.Value.GetString();
        }
        catch (JsonException) { }
        return null;
    }

    // ============================================================
    // CENTRAL DOWNLOAD
    // ============================================================

    public async Task<DocumentDownload> DownloadAsync(long attachmentId, CancellationToken ct = default)
    {
        var attachment = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId && a.IsActive && !a.IsDeleted, ct);
        // Missing, inactive, deleted and non-EA-document rows are indistinguishable to the caller.
        if (attachment is null || !Sources.Any(s => s.RelatedModule == attachment.RelatedModule && s.RelatedEntity == attachment.RelatedEntity))
            throw new NotFoundException("Document not found.");

        var absolute = ResolveStoragePath(attachment.ObjectKey);
        if (absolute is null || !File.Exists(absolute))
            throw new NotFoundException("Document file is not available.");

        var stream = new FileStream(absolute, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var contentType = !string.IsNullOrWhiteSpace(attachment.ContentType) && MediaTypeHeaderValue.TryParse(attachment.ContentType, out _)
            ? attachment.ContentType : "application/octet-stream";
        return new DocumentDownload(stream, contentType, attachment.OriginalFileName);
    }

    /// <summary>Resolves the stored key under {ContentRoot}/Content only; any key that escapes that folder (.., rooted, drive, UNC) resolves to null.</summary>
    private string? ResolveStoragePath(string? objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey)) return null;
        try
        {
            var root = Path.GetFullPath(Path.Combine(env.ContentRootPath, "Content")) + Path.DirectorySeparatorChar;
            var absolute = Path.GetFullPath(Path.Combine(env.ContentRootPath, objectKey.Replace('/', Path.DirectorySeparatorChar)));
            return absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? absolute : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
