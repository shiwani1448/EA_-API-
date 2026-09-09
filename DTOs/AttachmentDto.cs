namespace Jarvis5.Dtos;

public class AttachmentDto
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public long UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }

    /// <summary>Write-only input for a new attachment: the raw file content, base64
    /// encoded, as sent by the frontend. The server decodes this, writes the file to
    /// the Content folder, and derives FileUrl/FileSize from it — this field itself is
    /// never persisted to the database or returned in API responses.</summary>
    public string? Base64Content { get; set; }
}
