namespace Jarvis5.Dtos.EaFms;

public class NotificationResponseDto
{
    public long Id { get; set; }
    public string RecipientId { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? ReferenceModule { get; set; }
    public string? ReferenceId { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
