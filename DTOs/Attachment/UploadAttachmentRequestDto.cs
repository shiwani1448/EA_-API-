namespace Jarvis5.Dtos.Attachment;

/// <summary>Body for POST /api/attachments/upload. Frontend calls this once the
/// parent module (Request/Solution Design/Development Plan) has already been
/// saved, passing that module's EntityType/EntityId.</summary>
public class UploadAttachmentRequestDto
{
    public string EntityType { get; set; } = string.Empty;
    public long EntityId { get; set; }
    public long UploadedBy { get; set; }
    public List<AttachmentFileDto> Files { get; set; } = new();
}

/// <summary>One file within an upload request — raw base64 content, decoded and
/// written to disk server-side; never persisted to the database.</summary>
public class AttachmentFileDto
{
    public string OriginalName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public string Base64Content { get; set; } = string.Empty;
}
