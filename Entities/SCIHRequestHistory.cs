namespace Jarvis5.Entities;

public class SCIHRequestHistory
{
    public long Id { get; set; }
    public long RequestId { get; set; }
    public int Stage { get; set; }
    public string StageName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Raw JSON object holding the previous field value(s), or null.</summary>
    public string? PreviousValue { get; set; }

    /// <summary>Raw JSON object holding the new field value(s), or null.</summary>
    public string? NewValue { get; set; }

    public string? Remarks { get; set; }
    public long ActionBy { get; set; }
    public DateTime ActionDate { get; set; }
    public string? IPAddress { get; set; }
    public string? DeviceInfo { get; set; }
}
