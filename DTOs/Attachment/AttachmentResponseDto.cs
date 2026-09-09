namespace Jarvis5.Dtos.Attachment;

/// <summary>Response shape for upload / list — metadata only, never the file
/// content itself (use GET /api/attachments/download/{id} for that).</summary>
public class AttachmentResponseDto
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
}
