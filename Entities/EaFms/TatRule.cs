using System;

namespace Jarvis5.Entities.EaFms;

public class TatRule
{
    public long Id { get; set; }
    public int BusinessModuleId { get; set; }
    public string? OperationCode { get; set; }
    public int? PriorityLevelId { get; set; }
    public int Minutes { get; set; }
    public bool IsActive { get; set; }

    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
}
