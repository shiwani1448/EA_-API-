namespace Jarvis5.Dtos.EaFms;

public class CreateMeetingActionDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    /// <summary>Opaque stable doer identity. Optional — existing callers that send only
    /// OwnerName continue to work unchanged.</summary>
    public string? AssignedToId { get; set; }
    public string? OwnerName { get; set; }
    public int? PriorityLevelId { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Status { get; set; }
}
