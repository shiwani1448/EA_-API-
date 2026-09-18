namespace Jarvis5.Dtos.EaFms;

/// <summary>
/// Travel document response. Backed by the shared ea_attachments table
/// (Jarvis5.Entities.EaFms.Attachment) — no dedicated Travel document table.
/// Physical storage path/ObjectKey are intentionally not exposed; DownloadUrl
/// is the client-facing reference.
/// </summary>
public class TravelDocumentResponseDto
{
    public long Id { get; set; }
    public long TravelRequestId { get; set; }
    public string TravelReferenceNo { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long Size { get; set; }
    /// <summary>Free-form category (e.g. Itinerary, Quotation, Other) — not backend-mandatory.</summary>
    public string? DocumentCategory { get; set; }
    /// <summary>
    /// The Travel approval cycle this document was uploaded under, derived server-side from
    /// TravelRequest.CurrentCycleNo at upload time. Null when uploaded before first submission.
    /// </summary>
    public int? CycleNo { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
}
