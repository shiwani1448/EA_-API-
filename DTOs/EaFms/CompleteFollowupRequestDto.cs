using System;

namespace Jarvis5.Dtos.EaFms;

public class CompleteFollowupRequestDto
{
    // Client may supply business completion data; actor/time set server-side.
    public string? CompletionNote { get; set; }
    public string? OutcomeCode { get; set; }
    // CompletedAt remains server-controlled and will be ignored if supplied by client.
    public DateTime? CompletedAt { get; set; }
}
