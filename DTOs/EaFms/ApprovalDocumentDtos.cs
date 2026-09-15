namespace Jarvis5.Dtos.EaFms;

public class ApprovalDocumentResponseDto
{
    public long Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long Size { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public long ApprovalRequestId { get; set; }
    public long? ApprovalCycleId { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
}
