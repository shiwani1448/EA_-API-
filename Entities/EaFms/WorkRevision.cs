using System;

namespace Jarvis5.Entities.EaFms;

public class WorkRevision
{
    public long Id { get; set; }

    public long WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }

    public int RevisionNumber { get; set; }

    // Optional linkage to a business module record
    public long? BusinessModuleId { get; set; }
    public string? BusinessRecordId { get; set; }

    public string? RequestedById { get; set; }
    public string? RequestedByName { get; set; }
    public DateTime RequestedAt { get; set; }

    public string? Reason { get; set; }
    public string? ReviewerRemarks { get; set; }

    public string? RespondedById { get; set; }
    public string? RespondedByName { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? ResponseRemarks { get; set; }

    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedById { get; set; }
    public string? ResolvedByName { get; set; }

    public bool IsResolved { get; set; }
    public bool IsDeleted { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
