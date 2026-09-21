namespace Jarvis5.Dtos.EaFms;

public class UpdateIntakeRequestDto
{
    // See CreateIntakeRequestDto — same implicit-required rationale.
    public string? Title { get; set; }
    public string? Description { get; set; }
    public long? BusinessModuleId { get; set; }
    public int? StatusId { get; set; }
    public int? PriorityLevelId { get; set; }
    public string? Source { get; set; }
    public string? SourceChannel { get; set; }
    public string? SourceReferenceId { get; set; }
    public DateTime? RequiredDate { get; set; }
    public bool? IsConfidential { get; set; }
    public string? DoerId { get; set; }
}
