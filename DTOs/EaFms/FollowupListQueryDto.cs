namespace Jarvis5.Dtos.EaFms;

public class FollowupListQueryDto
{
    public long? BusinessModuleId { get; set; }
    public string? BusinessRecordId { get; set; }
    public string? AssignedToId { get; set; }
    public string? WaitingOnId { get; set; }
    public string? ResponseOwnerId { get; set; }
    public int? PriorityLevelId { get; set; }
    public string? Status { get; set; }
    public bool? IsCompleted { get; set; }
    public bool? IsOverdue { get; set; }
    public DateTime? DueFrom { get; set; }
    public DateTime? DueTo { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
