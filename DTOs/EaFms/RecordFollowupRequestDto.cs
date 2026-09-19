using System;

namespace Jarvis5.Dtos.EaFms;

public class RecordFollowupRequestDto
{
    public string? Note { get; set; }
    public DateTime? NextFollowupAt { get; set; }
    /// <summary>Frontend-supplied actor snapshot (operator's employee id). Stored as attribution; not verified.</summary>
    public string? EmployeeId { get; set; }
    /// <summary>Frontend-supplied actor snapshot (operator's employee name). Stored as attribution; not verified.</summary>
    public string? EmployeeName { get; set; }
    public DateTime? ExpectedResponseAt { get; set; }
    /// <summary>Optional outcome of this follow-up; stored on the history cycle.</summary>
    public string? OutcomeCode { get; set; }
}
