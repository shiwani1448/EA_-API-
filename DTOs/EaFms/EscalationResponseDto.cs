using System;

namespace Jarvis5.Dtos.EaFms;

public class EscalationResponseDto
{
    public long Id { get; set; }
    public long FollowupId { get; set; }
    public long? WorkflowInstanceId { get; set; }
    public long? IntakeRequestId { get; set; }
    public long? BusinessModuleId { get; set; }
    public string? BusinessRecordId { get; set; }
    public int EscalationLevelId { get; set; }
    public string? EscalationLevelName { get; set; }
    public int? EscalationLevelNumber { get; set; }
    public string EscalationState { get; set; } = "Open";
    public DateTime InitiatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Notes { get; set; }
    public string? EscalatedToId { get; set; }
    public string? EscalatedToName { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public string? AcknowledgedById { get; set; }
    public string? AcknowledgedByName { get; set; }
    public string? AcknowledgementNote { get; set; }
    public string? ResolvedBy { get; set; }
    public string? ResolvedById { get; set; }
    public string? ResolvedByName { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTime? NextEscalationAt { get; set; }
    public int? NextEscalationLevelId { get; set; }
    public string? NextEscalationLevelName { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
}
