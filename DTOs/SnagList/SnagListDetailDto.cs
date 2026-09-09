namespace Jarvis5.Dtos.SnagList;

/// <summary>Returned by Create/Update/UpdateStage/Close — field names mirror the
/// SCIH_SnagList table columns exactly (same shape convention as
/// Development Planning's TaskModuleDetailDto).</summary>
public class SnagListDetailDto
{
    public long Id { get; set; }
    public long? RequestId { get; set; }
    public long? TaskId { get; set; }
    public string Module { get; set; } = string.Empty;
    public string SnagDescription { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;

    /// <summary>Whatever JSON is stored in SCIH_SnagList.StageDetails, returned as-is —
    /// stage entries are stored verbatim from the client with no fixed schema.</summary>
    public object StageDetails { get; set; } = new List<object>();
    public DateTime CreationDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdationDate { get; set; }
    public string? UpdatedBy { get; set; }
    public string IsDelete { get; set; } = "false";
    public string? IsDeletedBy { get; set; }
}

/// <summary>Response for GET /api/snaglist/{id} — SnagDetails carries the snag's own
/// fields plus its stage details inline; History is the full audit trail.</summary>
public class SnagListDto
{
    public SnagListDetailDto SnagDetails { get; set; } = new();
    public List<SnagHistoryDto> History { get; set; } = new();
}
