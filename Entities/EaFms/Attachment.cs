namespace Jarvis5.Entities.EaFms;

/// <summary>
/// Attachment: stores file metadata only for EA FMS attachments. Actual file storage
/// (e.g. DigitalOcean Spaces) is handled separately.
/// </summary>
public class Attachment
{
    public long Id { get; set; }

    // Optional link to the module/entity this attachment belongs to
    public string? RelatedModule { get; set; }
    public string? RelatedEntity { get; set; }
    public string? RelatedEntityId { get; set; }

    // Original filename and stored object key (in object storage)
    public string OriginalFileName { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;

    public string? ContentType { get; set; }
    public long Size { get; set; }

    // Optional access URL or reference (populated when integrated with storage)
    public string? AccessUrl { get; set; }

    // Uploaded metadata
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }

    // Optional JSON metadata (e.g. extracted attributes)
    public string? Metadata { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    // Audit
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
