using System;

namespace Jarvis5.Dtos.EaFms;

public class IntakeClassificationResponseDto
{
    public long Id { get; set; }
    public long IntakeRequestId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
}
