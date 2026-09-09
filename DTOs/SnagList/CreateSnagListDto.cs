namespace Jarvis5.Dtos.SnagList;

/// <summary>Body for POST /api/snaglist — creates one SCIH_SnagList row, with its
/// selected stages (checklist loaded server-side from SCIH_StageMaster).</summary>
public class CreateSnagListDto
{
    /// <summary>Optional — a Snag List can be raised standalone with no request link.
    /// Not validated against SCIH_Request; stored as given.</summary>
    public long? RequestId { get; set; }

    /// <summary>Optional — SCIH_Task.Id of the Development Planning module this snag
    /// was raised against, if any. Not validated against SCIH_Task; stored as given.</summary>
    public long? TaskId { get; set; }

    public string Module { get; set; } = string.Empty;
    public string SnagDescription { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;

    /// <summary>Untyped passthrough — no fixed schema, no server-side validation or
    /// SCIH_StageMaster lookup. Stored exactly as sent; the frontend owns its shape
    /// and correctness entirely (same convention as CreateModuleDto.StageDetails).</summary>
    public List<object> StageDetails { get; set; } = new();
}
