namespace hrms_api.DTOs;

public class CallRecordingUploadDto
{
    public string FileBase64 { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public int? CandidateId { get; set; }
    public string? CallerName { get; set; }
    public string? Notes { get; set; }
    public int? DurationSeconds { get; set; }
    public DateTime? RecordedAt { get; set; }
}

public class CallRecordingResponseDto
{
    public int RecordingId { get; set; }
    public int? CandidateId { get; set; }
    public string? RecordingFileName { get; set; }
    public string RecordingPath { get; set; } = string.Empty;
    public string? RecordingContentType { get; set; }
    public int? DurationSeconds { get; set; }
    public string? CallerName { get; set; }
    public string? Notes { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
