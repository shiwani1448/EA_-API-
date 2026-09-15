using System.Globalization;
using System.Text.Json;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public class ApprovalDocumentService : IApprovalDocumentService
{
    private readonly EaFmsDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IAuditService _audit;
    private readonly IWebHostEnvironment _env;
    private readonly IApprovalAuthorizationService _auth;
    private readonly IApprovalLifecycleService _lifecycle;

    private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
    private const long MaxBytes = 25 * 1024 * 1024;

    public ApprovalDocumentService(EaFmsDbContext db, ICurrentUserService user, IAuditService audit, IWebHostEnvironment env, IApprovalAuthorizationService auth, IApprovalLifecycleService lifecycle)
    {
        _db = db; _user = user; _audit = audit; _env = env; _auth = auth; _lifecycle = lifecycle;
    }

    private async Task EnsureAuthorizedAsync(long approvalRequestId, string operation, CancellationToken ct = default)
    {
        if (!await _auth.CanPerformAsync(approvalRequestId, operation, ct))
            throw new BusinessRuleException("Current user is not authorized to perform this operation on the Approval request.");
    }

    public async Task<Jarvis5.Dtos.EaFms.ApprovalDocumentResponseDto> UploadAsync(long approvalRequestId, IFormFile file, long? approvalCycleId, CancellationToken ct = default)
    {
        if (file is null) throw new BusinessRuleException("File must be provided.");
        if (file.Length == 0) throw new BusinessRuleException("File must not be empty.");
        if (file.Length > MaxBytes) throw new BusinessRuleException("File exceeds maximum allowed size.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext)) throw new BusinessRuleException("File extension is not allowed.");

        // Resolve approval request (read-only pre-check)
        var request = await _db.ApprovalRequests.AsNoTracking().SingleOrDefaultAsync(a => a.Id == approvalRequestId && !a.IsDeleted, ct)
            ?? throw new NotFoundException($"ApprovalRequest {approvalRequestId} not found.");

        // Authorization: verify current user may operate on this request
        await EnsureAuthorizedAsync(approvalRequestId, "upload", ct);

        // Candidate resolved cycle id (pre-check) - must be revalidated inside transaction
        long candidateCycleId;
        if (approvalCycleId.HasValue)
        {
            var explicitCycle = await _db.ApprovalCycles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == approvalCycleId.Value, ct);
            if (explicitCycle is null || explicitCycle.ApprovalRequestId != approvalRequestId)
                throw new BusinessRuleException("Supplied ApprovalCycleId is invalid or does not belong to this ApprovalRequest.");

            // Verify explicit cycle is the current active cycle for the request (pre-check)
            var currentCycle = await _db.ApprovalCycles.AsNoTracking().Where(c => c.ApprovalRequestId == approvalRequestId).OrderByDescending(c => c.CycleNo).FirstOrDefaultAsync(ct);
            if (currentCycle is null || currentCycle.Id != explicitCycle.Id)
                throw new BusinessRuleException("Supplied ApprovalCycleId is not the current active cycle.");

            candidateCycleId = explicitCycle.Id;
        }
        else
        {
            if (request.CurrentCycleNo <= 0)
                throw new BusinessRuleException("Approval request has no current cycle; supply ApprovalCycleId explicitly.");
            var cycle = await _db.ApprovalCycles.AsNoTracking().FirstOrDefaultAsync(c => c.ApprovalRequestId == approvalRequestId && c.CycleNo == request.CurrentCycleNo, ct);
            if (cycle is null) throw new BusinessRuleException("Approval request has no current cycle; cannot upload document.");
            candidateCycleId = cycle.Id;
        }

        // Validate lifecycle via centralized lifecycle service
        var allowed = await _lifecycle.IsDocumentOperationAllowed(approvalRequestId, "upload", ct);
        if (!allowed) throw new BusinessRuleException("Documents cannot be uploaded in the current workflow state.");

        // Prepare storage key
        var generated = $"{Guid.NewGuid():N}{ext}";
        var key = Path.Combine("Content", "Approval", approvalRequestId.ToString(CultureInfo.InvariantCulture), generated).Replace(Path.DirectorySeparatorChar, '/');
        var absolute = Path.GetFullPath(Path.Combine(_env.ContentRootPath, key.Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "Content")) + Path.DirectorySeparatorChar;
        if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid storage key.");

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);

        // Write file first. If DB persistence fails later, we will delete the physical file to keep atomicity.
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

        // Persist attachment and audit inside a DB transaction and revalidate cycle/request consistency
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var req = await _db.ApprovalRequests.FirstOrDefaultAsync(a => a.Id == approvalRequestId && !a.IsDeleted, ct)
                      ?? throw new NotFoundException($"ApprovalRequest {approvalRequestId} not found.");

            var currentCycle = await _db.ApprovalCycles.Where(c => c.ApprovalRequestId == approvalRequestId).OrderByDescending(c => c.CycleNo).FirstOrDefaultAsync(ct)
                               ?? throw new BusinessRuleException("Approval request has no current cycle; cannot upload document.");

            if (currentCycle.Id != candidateCycleId)
                throw new BusinessRuleException("Approval cycle changed; supplied or resolved cycle is no longer current.");

            // Re-check lifecycle inside transaction
            var allowedInside = await _lifecycle.IsDocumentOperationAllowed(approvalRequestId, "upload", ct);
            if (!allowedInside) throw new BusinessRuleException("Documents cannot be uploaded in the current workflow state.");

            var now = Clock.UtcNowTz;
            var metadata = JsonSerializer.Serialize(new { ApprovalCycleId = candidateCycleId });

            var attachment = new Attachment
            {
                RelatedModule = "Approval",
                RelatedEntity = "ApprovalRequest",
                RelatedEntityId = approvalRequestId.ToString(CultureInfo.InvariantCulture),
                OriginalFileName = Path.GetFileName(file.FileName),
                ObjectKey = key,
                ContentType = file.ContentType,
                Size = file.Length,
                AccessUrl = "/" + key, // do not expose filesystem path
                UploadedBy = _user.UserName ?? _user.UserId.ToString(),
                UploadedAt = now,
                Metadata = metadata,
                IsActive = true,
                IsDeleted = false,
                CreatedBy = _user.UserName ?? _user.UserId.ToString(),
                CreatedDate = now
            };

            await _db.Set<Attachment>().AddAsync(attachment, ct);

            _audit.AddAudit("APPROVAL_DOCUMENT_UPLOADED", "Approval", nameof(ApprovalRequest), approvalRequestId.ToString(CultureInfo.InvariantCulture), null, new { approvalRequestId, CycleId = candidateCycleId, attachment.ObjectKey }, "Approval document uploaded");

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            // Return DTO
            return new Jarvis5.Dtos.EaFms.ApprovalDocumentResponseDto
            {
                Id = attachment.Id,
                OriginalFileName = attachment.OriginalFileName,
                ContentType = attachment.ContentType,
                Size = attachment.Size,
                UploadedBy = attachment.UploadedBy,
                UploadedAt = attachment.UploadedAt,
                ApprovalRequestId = approvalRequestId,
                ApprovalCycleId = candidateCycleId,
                DownloadUrl = attachment.AccessUrl
            };
        }
        catch
        {
            // Cleanup physical file if DB persistence failed
            try { if (File.Exists(absolute)) File.Delete(absolute); } catch { }
            throw;
        }
    }

    public async Task<List<Jarvis5.Dtos.EaFms.ApprovalDocumentResponseDto>> ListAsync(long approvalRequestId, CancellationToken ct = default)
    {
        await EnsureAuthorizedAsync(approvalRequestId, "list", ct);
        var rows = await _db.Set<Attachment>().AsNoTracking()
            .Where(a => a.RelatedModule == "Approval" && a.RelatedEntity == "ApprovalRequest" && a.RelatedEntityId == approvalRequestId.ToString(CultureInfo.InvariantCulture) && !a.IsDeleted)
            .OrderByDescending(a => a.UploadedAt)
            .ToListAsync(ct);

        return rows.Select(a => new Jarvis5.Dtos.EaFms.ApprovalDocumentResponseDto
        {
            Id = a.Id,
            OriginalFileName = a.OriginalFileName,
            ContentType = a.ContentType,
            Size = a.Size,
            UploadedBy = a.UploadedBy,
            UploadedAt = a.UploadedAt,
            ApprovalRequestId = approvalRequestId,
            ApprovalCycleId = TryExtractCycleId(a.Metadata),
            DownloadUrl = a.AccessUrl
        }).ToList();
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> DownloadAsync(long approvalRequestId, long documentId, CancellationToken ct = default)
    {
        await EnsureAuthorizedAsync(approvalRequestId, "download", ct);
        var att = await _db.Set<Attachment>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == documentId && !a.IsDeleted, ct)
                  ?? throw new NotFoundException("Document not found.");
        if (att.RelatedEntity != "ApprovalRequest" || att.RelatedEntityId != approvalRequestId.ToString(CultureInfo.InvariantCulture))
            throw new BusinessRuleException("Document does not belong to the specified approval request.");

        // Prevent download of deleted/soft-deleted files
        if (att.IsDeleted) throw new BusinessRuleException("Document has been deleted.");

        var absolute = Path.GetFullPath(Path.Combine(_env.ContentRootPath, att.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "Content")) + Path.DirectorySeparatorChar;
        if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid storage key.");
        if (!File.Exists(absolute)) throw new NotFoundException("Document file not found.");

        var bytes = await File.ReadAllBytesAsync(absolute, ct);
        return (bytes, att.ContentType ?? "application/octet-stream", att.OriginalFileName);
    }

    public async Task DeleteAsync(long approvalRequestId, long documentId, CancellationToken ct = default)
    {
        await EnsureAuthorizedAsync(approvalRequestId, "delete", ct);

        // Check lifecycle permission for delete
        var allowed = await _lifecycle.IsDocumentOperationAllowed(approvalRequestId, "delete", ct);
        if (!allowed) throw new BusinessRuleException("Documents cannot be deleted in the current workflow state.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var att = await _db.Set<Attachment>().FirstOrDefaultAsync(a => a.Id == documentId && !a.IsDeleted, ct)
                      ?? throw new NotFoundException("Document not found.");
            if (att.RelatedEntity != "ApprovalRequest" || att.RelatedEntityId != approvalRequestId.ToString(CultureInfo.InvariantCulture))
                throw new BusinessRuleException("Document does not belong to the specified approval request.");

            att.IsDeleted = true;
            att.IsActive = false;
            att.ModifiedBy = _user.UserName ?? _user.UserId.ToString();
            att.ModifiedDate = Clock.UtcNowTz;

            _db.Set<Attachment>().Update(att);
            _audit.AddAudit("APPROVAL_DOCUMENT_REMOVED", "Approval", nameof(ApprovalRequest), approvalRequestId.ToString(CultureInfo.InvariantCulture), null, new { approvalRequestId, DocumentId = att.Id }, "Approval document removed");

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            throw;
        }
    }

    private long? TryExtractCycleId(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata)) return null;
        try
        {
            using var doc = JsonDocument.Parse(metadata);
            if (doc.RootElement.TryGetProperty("ApprovalCycleId", out var prop) && prop.ValueKind == JsonValueKind.Number)
                return prop.GetInt64();
            if (doc.RootElement.TryGetProperty("ApprovalCycleId", out prop) && prop.ValueKind == JsonValueKind.String && long.TryParse(prop.GetString(), out var v))
                return v;
        }
        catch { }
        return null;
    }
}
