using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// Shared attachment convention for Approval's own review/rework decision attachments — deliberately
/// different from Delegation's (RelatedModule="Delegation"/RelatedEntity="Delegation"): matches the
/// EXISTING Approval documents convention instead (RelatedModule="Approval"/RelatedEntity=
/// "ApprovalRequest"), for consistency with ApprovalDocumentService's already-live uploads. Shared by
/// ApprovalLifecycleService (review/approve, review/rework, review/history) and ApprovalQueryService
/// (list/detail ReviewSummary.AttachmentId) so both read the exact same rows the exact same way.
/// </summary>
internal static class ApprovalAttachments
{
    public const string RelatedModule = "Approval";
    public const string RelatedEntity = "ApprovalRequest";
    public const string ReviewAttachmentPurpose = "ApprovalReviewAttachment";
    public const string ReworkAttachmentPurpose = "ApprovalReworkAttachment";

    public sealed record Row(long Id, long ApprovalRequestId, string? Purpose, int? ReviewCycleNumber);

    /// <summary>Every active ea_attachments row for the given Approval Requests, in one query.</summary>
    public static async Task<List<Row>> LoadRowsAsync(EaFmsDbContext db, IReadOnlyCollection<long> approvalRequestIds, CancellationToken ct)
    {
        if (approvalRequestIds.Count == 0) return new();
        var keys = approvalRequestIds.Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
        var rows = await db.Attachments.AsNoTracking()
            .Where(a => a.RelatedModule == RelatedModule && a.RelatedEntity == RelatedEntity
                && a.RelatedEntityId != null && keys.Contains(a.RelatedEntityId) && a.IsActive && !a.IsDeleted)
            .Select(a => new { a.Id, a.RelatedEntityId, a.Metadata })
            .ToListAsync(ct);
        return rows.Select(r =>
        {
            var (purpose, cycle) = ParseMetadata(r.Metadata);
            return new Row(r.Id, long.Parse(r.RelatedEntityId!, System.Globalization.CultureInfo.InvariantCulture), purpose, cycle);
        }).ToList();
    }

    /// <summary>Same best-effort JSON-metadata reading Delegation uses — malformed/missing Metadata just yields nulls, never throws.</summary>
    private static (string? Purpose, int? ReviewCycleNumber) ParseMetadata(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata)) return (null, null);
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(metadata);
            var purpose = doc.RootElement.TryGetProperty("purpose", out var p) ? p.GetString() : null;
            var cycle = doc.RootElement.TryGetProperty("reviewCycleNumber", out var c) && c.TryGetInt32(out var n) ? (int?)n : null;
            return (purpose, cycle);
        }
        catch (System.Text.Json.JsonException) { return (null, null); }
    }

    /// <summary>Latest review/rework decision attachment id per (ApprovalRequest, review cycle).</summary>
    public static Dictionary<(long ApprovalRequestId, int Cycle), long> ReviewAttachmentIds(IEnumerable<Row> rows) =>
        rows.Where(r => (r.Purpose == ReviewAttachmentPurpose || r.Purpose == ReworkAttachmentPurpose) && r.ReviewCycleNumber.HasValue)
            .GroupBy(r => (r.ApprovalRequestId, Cycle: r.ReviewCycleNumber!.Value))
            .ToDictionary(g => g.Key, g => g.Max(r => r.Id));

    /// <summary>Sets review.AttachmentId from the batch loaded above. No-op when never submitted (cycle 0).</summary>
    public static void ApplyReviewAttachment(TaskReviewSummaryDto? review, long approvalRequestId, Dictionary<(long ApprovalRequestId, int Cycle), long> reviewAttachmentIds)
    {
        if (review is null || review.ReviewCycleNumber <= 0) return;
        review.AttachmentId = reviewAttachmentIds.TryGetValue((approvalRequestId, review.ReviewCycleNumber), out var id) ? id : null;
    }
}
