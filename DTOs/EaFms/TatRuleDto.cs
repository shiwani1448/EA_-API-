using System;

namespace Jarvis5.Dtos.EaFms;

public class TatRuleDto
{
    public long Id { get; set; }
    public int BusinessModuleId { get; set; }
    public string? OperationCode { get; set; }
    public int? PriorityLevelId { get; set; }
    public int Minutes { get; set; }
    public bool IsActive { get; set; }
}
