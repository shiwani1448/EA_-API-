using System;
using System.Collections.Generic;

namespace Jarvis5.Dtos.EaFms;

public class WorkflowResponseDto
{
    public long Id { get; set; }
    public long? IntakeRequestId { get; set; }
    public long? BusinessModuleId { get; set; }
    public string? BusinessRecordId { get; set; }
    public int StatusId { get; set; }
    public string? StatusName { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? TatStartedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public List<WorkflowHistoryResponseDto> History { get; set; } = new();
}
