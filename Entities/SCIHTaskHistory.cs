namespace Jarvis5.Entities;

/// <summary>Full audit trail for a Task module — module create/update/delete
/// and per-stage changes (doer, dates, status, checklist, remarks). Nothing is
/// ever deleted from this table.</summary>
public class SCIHTaskHistory
{
    public long Id { get; set; }
    public long TaskId { get; set; }

    /// <summary>Null when the row belongs to a standalone Snag List raised with no
    /// request link.</summary>
    public long? RequestId { get; set; }

    /// <summary>Null for module-level changes (e.g. module created/deleted); set
    /// when the change is scoped to a single stage inside StageDetailsJson.</summary>
    public long? StageId { get; set; }

    public string? StageName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Raw JSON holding the previous field value(s), or null.</summary>
    public string? PreviousValue { get; set; }

    /// <summary>Raw JSON holding the new field value(s), or null.</summary>
    public string? NewValue { get; set; }

    public string? Remarks { get; set; }
    public long ActionBy { get; set; }
    public DateTime ActionDate { get; set; }
    public string? IPAddress { get; set; }
    public string? DeviceInfo { get; set; }
}
