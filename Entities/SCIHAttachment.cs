namespace Jarvis5.Entities;

/// <summary>Shared attachment table for every module (Request, Solution Design,
/// Development Plan, …) — a module is identified by (EntityType, EntityId) rather
/// than a DB foreign key, since it must stay independent of any single module's
/// schema.</summary>
public class SCIHAttachment
{
    public long Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public long EntityId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public long UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}
