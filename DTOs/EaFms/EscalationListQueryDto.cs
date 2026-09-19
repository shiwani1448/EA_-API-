namespace Jarvis5.Dtos.EaFms;

public sealed class EscalationListQueryDto
{
    public long? FollowupId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}