namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One row per AI (Claude) call made from EA FMS — written every time, whether or not the EA
/// later accepts the suggestion. Records who used AI, where (module / feature / endpoint), for
/// what (record + central task), the prompt, the full response, the tokens and the outcome.
/// </summary>
public class EaAiUsageLog
{
    public long Id { get; set; }

    /// <summary>Meeting | Delegation | Approval | Travel | Follow-up | Calendar.</summary>
    public string Module { get; set; } = string.Empty;
    /// <summary>The AI feature, e.g. "Delegation delay risk check".</summary>
    public string Feature { get; set; } = string.Empty;
    /// <summary>The API call the frontend made, e.g. "POST /api/ea/delegations/12/ai/delay-risk".</summary>
    public string? Endpoint { get; set; }

    /// <summary>The module record the AI was used for (meeting id, delegation id, …); null for unsaved forms.</summary>
    public string? BusinessRecordId { get; set; }
    /// <summary>The central task (ea_tasks.Id) of that record, when it has one.</summary>
    public long? EaTaskId { get; set; }

    /// <summary>The EA who used AI (from the login, else the X-Employee-Id/X-Employee-Name headers).</summary>
    public string? RequestedById { get; set; }
    public string? RequestedByName { get; set; }

    public DateTime RequestedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public int DurationMs { get; set; }
    /// <summary>1 for the first try; 2+ when the previous reply was not valid JSON and the call was retried.</summary>
    public int AttemptNo { get; set; }

    /// <summary>Succeeded | InvalidJson | Failed.</summary>
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }

    public string? Model { get; set; }
    public string? MessageId { get; set; }
    public string? StopReason { get; set; }

    public string? SystemPrompt { get; set; }
    public string? UserPrompt { get; set; }
    /// <summary>The full AI response text.</summary>
    public string? ResponseText { get; set; }

    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long? CacheCreationInputTokens { get; set; }
    public long? CacheReadInputTokens { get; set; }
    public long? TotalTokens { get; set; }

    /// <summary>true once the EA used this response (applied / confirmed / sent it, or the screen reported it).</summary>
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedById { get; set; }
    public string? UsedByName { get; set; }
    /// <summary>What she actually used (JSON) — shows whether she kept the AI's answer or changed it.</summary>
    public string? UsedValue { get; set; }

    public DateTime CreatedDate { get; set; }
}
