namespace Jarvis5.Dtos.EaFms;

public class CreateMeetingActionDto
{
    public long? MeetingActionId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    /// <summary>Opaque stable doer identity. Optional — existing callers that send only
    /// DoerName continue to work unchanged.</summary>
    public string? DoerId { get; set; }
    public string? DoerName { get; set; }
    public string? Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Status { get; set; }
}
