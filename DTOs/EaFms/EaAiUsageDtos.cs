namespace Jarvis5.Dtos.EaFms;

/// <summary>Filters for the EA AI usage log. All optional.</summary>
public class EaAiUsageQueryDto
{
    /// <summary>Meeting | Delegation | Approval | Travel | Follow-up | Calendar.</summary>
    public string? Module { get; set; }
    /// <summary>Contains-match on the feature name, e.g. "delay risk".</summary>
    public string? Feature { get; set; }
    /// <summary>Succeeded | InvalidJson | Failed.</summary>
    public string? Status { get; set; }
    public string? BusinessRecordId { get; set; }
    public long? EaTaskId { get; set; }
    /// <summary>The EA who used AI (matched on the stored id or name).</summary>
    public string? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    /// <summary>true = only responses the EA used; false = only ones she did not use.</summary>
    public bool? Used { get; set; }
    /// <summary>Inclusive UTC range on RequestedAt.</summary>
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    /// <summary>1–200, default 50.</summary>
    public int PageSize { get; set; } = 50;
}

/// <summary>One AI call in a list (prompt and response are in the detail endpoint).</summary>
public class EaAiUsageRowDto
{
    public long Id { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Feature { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public string? BusinessRecordId { get; set; }
    public long? EaTaskId { get; set; }
    public string? RequestedById { get; set; }
    public string? RequestedByName { get; set; }
    public DateTime RequestedAt { get; set; }
    public int DurationMs { get; set; }
    public int AttemptNo { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Model { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long? TotalTokens { get; set; }
    /// <summary>Did the EA use this response?</summary>
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedByName { get; set; }
}

/// <summary>One AI call in full: prompt, complete response, tokens and outcome.</summary>
public class EaAiUsageDetailDto : EaAiUsageRowDto
{
    public DateTime CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? MessageId { get; set; }
    public string? StopReason { get; set; }
    public string? SystemPrompt { get; set; }
    public string? UserPrompt { get; set; }
    public string? ResponseText { get; set; }
    public long? CacheCreationInputTokens { get; set; }
    public long? CacheReadInputTokens { get; set; }
    public string? UsedById { get; set; }
    /// <summary>What she actually used (JSON), e.g. {"doerId":"E-9","doerName":"Riya"} — compare with ResponseText.</summary>
    public string? UsedValue { get; set; }
}

/// <summary>Body for POST /api/ea/ai-usage/{id}/used (optional).</summary>
public class MarkAiUsageUsedRequestDto
{
    /// <summary>What the EA used (free text or JSON), e.g. the itinerary text she copied.</summary>
    public string? UsedValue { get; set; }
}

/// <summary>Call and token totals for a group (a module, feature, EA, task or day).</summary>
public class EaAiUsageTotalsDto
{
    public string Key { get; set; } = string.Empty;
    public int Calls { get; set; }
    public int Succeeded { get; set; }
    public int InvalidJson { get; set; }
    public int Failed { get; set; }
    /// <summary>Responses the EA used.</summary>
    public int Used { get; set; }
    /// <summary>Successful responses she did not use.</summary>
    public int NotUsed { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long TotalTokens { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public class EaAiUsageSummaryDto
{
    public EaAiUsageTotalsDto Total { get; set; } = new() { Key = "Total" };
    public List<EaAiUsageTotalsDto> ByModule { get; set; } = new();
    public List<EaAiUsageTotalsDto> ByFeature { get; set; } = new();
    public List<EaAiUsageTotalsDto> ByEmployee { get; set; } = new();
    /// <summary>Per task: key = "Module:RecordId" (EaTaskId shown on each row when known). Most tokens first, top 100.</summary>
    public List<EaAiTaskUsageDto> ByTask { get; set; } = new();
    /// <summary>Per India calendar day (yyyy-MM-dd).</summary>
    public List<EaAiUsageTotalsDto> ByDay { get; set; } = new();
}

public class EaAiTaskUsageDto : EaAiUsageTotalsDto
{
    public string Module { get; set; } = string.Empty;
    public string? BusinessRecordId { get; set; }
    public long? EaTaskId { get; set; }
}

/// <summary>Everything AI did for one task: totals plus every call, oldest first.</summary>
public class EaAiTaskUsageDetailDto
{
    public long EaTaskId { get; set; }
    public EaAiUsageTotalsDto Total { get; set; } = new();
    public List<EaAiUsageTotalsDto> ByFeature { get; set; } = new();
    public List<EaAiUsageRowDto> Calls { get; set; } = new();
}
