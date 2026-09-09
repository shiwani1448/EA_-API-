using System.Text.Json;

namespace hrms_api.Models;

public class Sourcing
{
    public int SourcingId { get; set; }
    public int HiringRequestId { get; set; }
    public string? Source { get; set; }
    public string? SubSource { get; set; }
    public JsonDocument? TaskDetails { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
