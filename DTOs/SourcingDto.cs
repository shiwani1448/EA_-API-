using System.Text.Json;

namespace hrms_api.DTOs;

public class SourcingDto
{
    public int HiringRequestId { get; set; }
    public string? Source { get; set; }
    public string? SubSource { get; set; }
    public JsonElement? TaskDetails { get; set; }
    public string? Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}
