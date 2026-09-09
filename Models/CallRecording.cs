namespace hrms_api.Models;

public class CallRecording
{
    public int RecordingId { get; set; }
    public int? CandidateId { get; set; }
    public string? RecordingFileName { get; set; }
    public string RecordingPath { get; set; } = string.Empty;
    public string? RecordingContentType { get; set; }
    public int? DurationSeconds { get; set; }
    public string? CallerName { get; set; }
    public string? Notes { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
