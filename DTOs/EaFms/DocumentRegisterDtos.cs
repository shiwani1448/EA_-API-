namespace Jarvis5.Dtos.EaFms;

/// <summary>
/// Filters for the central Document &amp; Records register. All optional. The register is a read model over ea_attachments
/// (active, non-deleted rows of the EA modules that upload documents); it never writes.
/// </summary>
public class DocumentRegisterQueryDto
{
    /// <summary>Canonical BusinessModule.Id (never compared with the attachment's RelatedModule text).</summary>
    public long? BusinessModuleId { get; set; }
    /// <summary>Normalized document type, case-insensitive exact match (e.g. "Completion PDF", "Approval Document", or a Travel document category).</summary>
    public string? Type { get; set; }
    /// <summary>Case-insensitive exact match on Attachment.UploadedBy.</summary>
    public string? UploadedBy { get; set; }
    /// <summary>Attachment.UploadedAt on/after this India calendar day.</summary>
    public DateTime? FromDate { get; set; }
    /// <summary>Attachment.UploadedAt on/before this India calendar day.</summary>
    public DateTime? ToDate { get; set; }
    /// <summary>Case-insensitive contains over file name, task (EaTask.Task), module name and document type.</summary>
    public string? Search { get; set; }
    /// <summary>1-based page number (default 1).</summary>
    public int Page { get; set; } = 1;
    /// <summary>Rows per page (default 50, maximum 200).</summary>
    public int PageSize { get; set; } = 50;
}

/// <summary>One real ea_attachments row, enriched with its canonical module and central task. Never exposes the storage key or path.</summary>
public class DocumentRegisterRowDto
{
    /// <summary>ea_attachments.Id.</summary>
    public long AttachmentId { get; set; }
    /// <summary>Canonical BusinessModule.Id of the source module.</summary>
    public long ModuleId { get; set; }
    /// <summary>Canonical BusinessModule.Name (e.g. "Travel &amp; Hospitality", "EA Approval").</summary>
    public string ModuleName { get; set; } = string.Empty;
    /// <summary>Normalized document type derived from the attachment (read-model mapping only; not stored).</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>The central EaTask of the source record, when one exists.</summary>
    public long? EaTaskId { get; set; }
    /// <summary>EaTask business identity: Meeting.Id, Delegation.Id, TravelRequest.Id or ApprovalRequest.ReferenceNo.</summary>
    public string? BusinessRecordId { get; set; }
    /// <summary>EaTask.Task of the source record; null when the record has no EaTask.</summary>
    public string? TaskDescription { get; set; }
    /// <summary>Attachment.UploadedAt (server time).</summary>
    public DateTime UploadTime { get; set; }
    /// <summary>Attachment.UploadedBy exactly as stored by the source module.</summary>
    public string UploadedBy { get; set; } = string.Empty;
    public DocumentFileDto Document { get; set; } = new();
}

public class DocumentFileDto
{
    public long AttachmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long Size { get; set; }
    /// <summary>Central download route (GET /api/ea/documents/{attachmentId}/download).</summary>
    public string DownloadUrl { get; set; } = string.Empty;
}
