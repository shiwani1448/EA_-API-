using System;

namespace Jarvis5.Dtos.EaFms;

public class IntakeRequestResponseDto
{
    // Provide explicit friendly identifier for frontend
    public long IntakeRequestId { get; set; }
    // keep legacy Id for internal/back-compat mapping if needed
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long? BusinessModuleId { get; set; }
    public string? BusinessModuleName { get; set; }
    public int? StatusId { get; set; }
    public string? StatusName { get; set; }
    public int? PriorityLevelId { get; set; }
    public string? PriorityLevelName { get; set; }
    public string? Source { get; set; }
    public string? SourceChannel { get; set; }
    public string? SourceReferenceId { get; set; }
    // Use RequiredDate (frontend contract requires this field name)
    public DateTime? RequiredDate { get; set; }
    public bool IsConfidential { get; set; }
    public string? DoerId { get; set; }
    public string? DoerName { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    // concise current summaries where implemented
    public string? WorkflowSummary { get; set; }
    public string? AssignmentSummary { get; set; }
    public string? FollowupSummary { get; set; }
    public string? EscalationSummary { get; set; }
}
