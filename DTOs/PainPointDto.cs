namespace Jarvis5.Dtos;

public class PainPointDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Severity { get; set; } = "Low";
    public string Frequency { get; set; } = "Daily";
    public string? Remarks { get; set; }
}
