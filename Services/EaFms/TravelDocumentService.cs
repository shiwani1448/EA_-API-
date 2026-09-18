using System.Globalization;
using System.Text.Json;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// Travel document upload/list/download/delete, backed entirely by the shared
/// ea_attachments table (Jarvis5.Entities.EaFms.Attachment) via RelatedModule/
/// RelatedEntity/RelatedEntityId — mirrors ApprovalDocumentService's storage,
/// validation and soft-delete conventions. No dedicated Travel document table.
///
/// Category and the applicable approval cycle number have no dedicated columns on
/// Attachment, so — exactly like ApprovalDocumentService already does for
/// ApprovalCycleId — they are stored as JSON in the existing Metadata field.
/// CycleNo is always derived server-side from TravelRequest.CurrentCycleNo; the
/// frontend never supplies a cycle identifier.
/// </summary>
public class TravelDocumentService : ITravelDocumentService
{
    private const string ModuleName = "Travel";
    private const string EntityName = "TravelRequest";

    private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
    private const long MaxBytes = 25 * 1024 * 1024;
    private const int MaxCategoryLength = 100;

    private readonly EaFmsDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IAuditService _audit;
    private readonly IWebHostEnvironment _env;

    public TravelDocumentService(EaFmsDbContext db, ICurrentUserService user, IAuditService audit, IWebHostEnvironment env)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _env = env;
    }

    public async Task<TravelDocumentResponseDto> UploadAsync(
        long travelRequestId, IFormFile file, string? documentCategory, CancellationToken ct = default)
    {
        if (file is null) throw new BusinessRuleException("File must be provided.");
        if (file.Length == 0) throw new BusinessRuleException("File must not be empty.");
        if (file.Length > MaxBytes) throw new BusinessRuleException("File exceeds maximum allowed size.");

        var originalFileName = Path.GetFileName(file.FileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(originalFileName) || originalFileName.Length > 500)
            throw new BusinessRuleException("File name must contain 1 to 500 characters.");
        if (file.ContentType?.Length > 200)
            throw new BusinessRuleException("Content type must not exceed 200 characters.");
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext)) throw new BusinessRuleException("File extension is not allowed.");

        var category = string.IsNullOrWhiteSpace(documentCategory) ? null : documentCategory.Trim();
        if (category?.Length > MaxCategoryLength)
            throw new BadRequestException($"documentCategory must not exceed {MaxCategoryLength} characters.");

        // Step 1 of the upload contract: TravelRequest must exist and not be deleted.
        var travelExists = await _db.TravelRequests.AsNoTracking()
            .AnyAsync(t => t.Id == travelRequestId && !t.IsDeleted, ct);
        if (!travelExists) throw new NotFoundException($"Travel request {travelRequestId} not found.");

        // Actor must be an authenticated user; anonymous uploads ("0") are not permitted.
        if (_user.UserId == 0 && _user.UserName is null)
            throw new BusinessRuleException("Authenticated user identity is required to upload a Travel document.");

        // Prepare storage key (same layout convention as Approval documents).
        var generated = $"{Guid.NewGuid():N}{ext}";
        var key = Path.Combine("Content", "Travel", travelRequestId.ToString(CultureInfo.InvariantCulture), generated)
            .Replace(Path.DirectorySeparatorChar, '/');
        var absolute = Path.GetFullPath(Path.Combine(_env.ContentRootPath, key.Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "Content")) + Path.DirectorySeparatorChar;
        if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid storage key.");

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);

        // Write the file first; if DB persistence fails, delete the physical file to stay atomic.
        try
        {
            await using var stream = new FileStream(absolute, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
            await file.CopyToAsync(stream, ct);
            await stream.FlushAsync(ct);
        }
        catch (Exception)
        {
            if (File.Exists(absolute)) File.Delete(absolute);
            throw;
        }

        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            // Re-check inside the transaction in case the request was deleted concurrently.
            var travel = await _db.TravelRequests.FirstOrDefaultAsync(t => t.Id == travelRequestId && !t.IsDeleted, ct)
                ?? throw new NotFoundException($"Travel request {travelRequestId} not found.");

            var now = Clock.UtcNowTz;
            // CurrentCycleNo == 0 means no approval cycle exists yet (pre-submission draft) —
            // the document belongs to the TravelRequest with no cycle association, per spec.
            int? cycleNo = travel.CurrentCycleNo > 0 ? travel.CurrentCycleNo : null;
            var metadata = JsonSerializer.Serialize(new { DocumentCategory = category, CycleNo = cycleNo });

            var attachment = new Attachment
            {
                RelatedModule = ModuleName,
                RelatedEntity = EntityName,
                RelatedEntityId = travelRequestId.ToString(CultureInfo.InvariantCulture),
                OriginalFileName = originalFileName,
                ObjectKey = key,
                ContentType = file.ContentType,
                Size = file.Length,
                AccessUrl = null, // Downloads use the ownership-checked Travel route.
                UploadedBy = _user.UserName ?? _user.UserId.ToString(CultureInfo.InvariantCulture),
                UploadedAt = now,
                Metadata = metadata,
                IsActive = true,
                IsDeleted = false,
                CreatedBy = _user.UserName ?? _user.UserId.ToString(CultureInfo.InvariantCulture),
                CreatedDate = now
            };

            await _db.Set<Attachment>().AddAsync(attachment, ct);

            _audit.AddAudit(
                "TRAVEL_DOCUMENT_UPLOAD", ModuleName, EntityName,
                travelRequestId.ToString(CultureInfo.InvariantCulture), null,
                new { travelRequestId, travel.ReferenceNo, DocumentCategory = category, CycleNo = cycleNo, attachment.OriginalFileName },
                "Travel document uploaded");

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ToDto(attachment, travelRequestId, travel.ReferenceNo);
        }
        catch
        {
            try { if (File.Exists(absolute)) File.Delete(absolute); } catch { /* best-effort cleanup */ }
            throw;
        }
    }

    public async Task<List<TravelDocumentResponseDto>> ListAsync(long travelRequestId, CancellationToken ct = default)
    {
        var travel = await _db.TravelRequests.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == travelRequestId && !t.IsDeleted, ct)
            ?? throw new NotFoundException($"Travel request {travelRequestId} not found.");

        var recordId = travelRequestId.ToString(CultureInfo.InvariantCulture);
        var rows = await _db.Set<Attachment>().AsNoTracking()
            .Where(a => a.RelatedModule == ModuleName && a.RelatedEntity == EntityName
                && a.RelatedEntityId == recordId && a.IsActive && !a.IsDeleted)
            .OrderByDescending(a => a.UploadedAt)
            .ThenByDescending(a => a.Id)
            .ToListAsync(ct);

        return rows.Select(a => ToDto(a, travelRequestId, travel.ReferenceNo)).ToList();
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> DownloadAsync(long documentId, CancellationToken ct = default)
    {
        var att = await _db.Set<Attachment>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == documentId, ct);
        // A missing row, a soft-deleted row, and a row belonging to another module are all
        // reported identically (404) so a Travel caller can never distinguish "not a Travel
        // document" from "doesn't exist" for a shared attachment ID it doesn't own.
        if (att is null || !att.IsActive || att.IsDeleted || att.RelatedModule != ModuleName || att.RelatedEntity != EntityName)
            throw new NotFoundException("Travel document not found.");

        await EnsureParentExistsAsync(att, ct);

        var absolute = Path.GetFullPath(Path.Combine(_env.ContentRootPath, att.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "Content", "Travel", att.RelatedEntityId!)) + Path.DirectorySeparatorChar;
        if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid storage key.");
        if (!File.Exists(absolute)) throw new NotFoundException("Travel document file not found.");

        var bytes = await File.ReadAllBytesAsync(absolute, ct);
        return (bytes, att.ContentType ?? "application/octet-stream", att.OriginalFileName);
    }

    public async Task DeleteAsync(long documentId, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var att = await _db.Set<Attachment>().FirstOrDefaultAsync(a => a.Id == documentId, ct);
        if (att is null || !att.IsActive || att.IsDeleted || att.RelatedModule != ModuleName || att.RelatedEntity != EntityName)
            throw new NotFoundException("Travel document not found.");

        await EnsureParentExistsAsync(att, ct);

        // Soft delete only — metadata/history preserved, physical file left in place.
        // Never touches TravelRequest/EaTask/TravelRequestCycle or any Travel state.
        att.IsDeleted = true;
        att.IsActive = false;
        att.ModifiedBy = _user.UserName ?? _user.UserId.ToString(CultureInfo.InvariantCulture);
        att.ModifiedDate = Clock.UtcNowTz;

        _audit.AddAudit(
            "TRAVEL_DOCUMENT_DELETE", ModuleName, EntityName,
            att.RelatedEntityId ?? "", null,
            new { DocumentId = att.Id, TravelRequestId = att.RelatedEntityId },
            "Travel document removed");

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task EnsureParentExistsAsync(Attachment attachment, CancellationToken ct)
    {
        if (!long.TryParse(attachment.RelatedEntityId, NumberStyles.None, CultureInfo.InvariantCulture, out var requestId)
            || requestId <= 0
            || !await _db.TravelRequests.AsNoTracking().AnyAsync(t => t.Id == requestId && !t.IsDeleted, ct))
            throw new NotFoundException("Travel document not found.");
    }

    private static TravelDocumentResponseDto ToDto(Attachment a, long travelRequestId, string referenceNo)
    {
        var (category, cycleNo) = ParseMetadata(a.Metadata);
        return new TravelDocumentResponseDto
        {
            Id = a.Id,
            TravelRequestId = travelRequestId,
            TravelReferenceNo = referenceNo,
            OriginalFileName = a.OriginalFileName,
            ContentType = a.ContentType,
            Size = a.Size,
            DocumentCategory = category,
            CycleNo = cycleNo,
            UploadedBy = a.UploadedBy,
            UploadedAt = a.UploadedAt,
            IsActive = a.IsActive,
            IsDeleted = a.IsDeleted,
            DownloadUrl = $"/api/ea/travel/documents/{a.Id}"
        };
    }

    private static (string? Category, int? CycleNo) ParseMetadata(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata)) return (null, null);
        try
        {
            using var doc = JsonDocument.Parse(metadata);
            string? category = doc.RootElement.TryGetProperty("DocumentCategory", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString()
                : null;
            int? cycleNo = doc.RootElement.TryGetProperty("CycleNo", out var n) && n.ValueKind == JsonValueKind.Number
                ? n.GetInt32()
                : null;
            return (category, cycleNo);
        }
        catch
        {
            return (null, null);
        }
    }
}
