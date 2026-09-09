namespace Jarvis5.Dtos.EaFms;

public class CreateAssignmentRequestDto
{
    public string? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public string? AssignmentType { get; set; }
    public string? Reason { get; set; }
}
