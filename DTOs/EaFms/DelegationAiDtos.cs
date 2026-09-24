namespace Jarvis5.Dtos.EaFms;

// ============================================================
// Suggest owner (preview only — advisory; no employee/role directory exists)
// ============================================================

/// <summary>Response for POST /api/ea/delegations/{delegationId}/ai/suggest-owner.
/// Purely a pattern read from past Delegations of the same DelegationType — this system
/// has no employee/role directory, so this can never be an assignment rule and never names
/// anyone who wasn't a real doer of a past delegation of the same type.</summary>
public class DelegationAiOwnerSuggestionResponseDto
{
    public long DelegationId { get; set; }

    /// <summary>Null when there is no history to draw from — deliberately never guessed.</summary>
    public string? SuggestedDoerId { get; set; }
    public string? SuggestedDoerName { get; set; }

    /// <summary>How many past delegations of the same DelegationType this is based on.
    /// 0 means SuggestedDoerId is null.</summary>
    public int HistoricalSampleSize { get; set; }
    public string? Reasoning { get; set; }
    public string? WarningMessage { get; set; }
}

/// <summary>Request for POST /api/ea/delegations/{delegationId}/ai/suggest-owner/apply. The
/// EA has reviewed the suggestion (or typed their own choice) and wants it written onto the
/// real delegation — this never re-calls Claude, it only persists the value given here.</summary>
public class ApplySuggestedOwnerRequestDto
{
    public string DoerId { get; set; } = string.Empty;
    public string? DoerName { get; set; }
}

// ============================================================
// Predict due date (preview only — grounded in configured TAT or real history, never invented)
// ============================================================

/// <summary>Response for POST /api/ea/delegations/{delegationId}/ai/predict-due-date. The
/// date itself is always computed in code from a real number (a configured TAT rule or the
/// real average of past completions) — Claude only phrases the explanation, it never
/// performs the date arithmetic itself.</summary>
public class DelegationAiDueDatePredictionResponseDto
{
    public long DelegationId { get; set; }

    /// <summary>Null when there is nothing to estimate from (see Basis).</summary>
    public DateTime? SuggestedDueDate { get; set; }

    /// <summary>ConfiguredTat | HistoricalAverage | None.</summary>
    public string Basis { get; set; } = "None";
    public string? Explanation { get; set; }
    public string? WarningMessage { get; set; }
}

/// <summary>Request for POST /api/ea/delegations/{delegationId}/ai/predict-due-date/apply.
/// The EA has reviewed the suggested date (or typed their own) and wants it written onto the
/// real delegation's EndDate — this never re-calls Claude or recomputes anything, it only
/// persists the date given here.</summary>
public class ApplyPredictedDueDateRequestDto
{
    public DateTime EndDate { get; set; }
}

// ============================================================
// Delay risk check (preview only — reads real execution data already on the record)
// ============================================================

/// <summary>Response for POST /api/ea/delegations/{delegationId}/ai/delay-risk. Every input
/// (status, due date proximity, TAT usage, pause history) is real, already-computed data
/// from the Delegation's own record — nothing is invented.</summary>
public class DelegationAiDelayRiskResponseDto
{
    public long DelegationId { get; set; }

    /// <summary>Low | Medium | High.</summary>
    public string RiskLevel { get; set; } = "Low";
    public string? Reasoning { get; set; }

    /// <summary>A drafted nudge/reminder message the EA can copy and send manually — null
    /// when no nudge is warranted (e.g. Low risk). This never sends or schedules anything
    /// itself; there is no reminder/escalation feature wired to Delegation to attach it to.</summary>
    public string? SuggestedNudgeMessage { get; set; }
}
