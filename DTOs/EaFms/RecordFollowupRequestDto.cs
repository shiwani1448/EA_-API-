using System;

namespace Jarvis5.Dtos.EaFms;

public class RecordFollowupRequestDto
{
    public string? Note { get; set; }
    public DateTime? NextFollowupAt { get; set; }
    public DateTime? ExpectedResponseAt { get; set; }
}
