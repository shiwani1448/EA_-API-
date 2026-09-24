namespace Jarvis5.Dtos.EaFms;

// ============================================================
// Auto reminders (suggest, then EA-confirmed send via the real IEaReminderEmailSender)
// ============================================================

/// <summary>Response for POST /api/ea/followups/{followupId}/ai/reminder. A draft only —
/// nothing is sent until the EA reviews and calls reminder/send.</summary>
public class FollowupAiReminderSuggestionResponseDto
{
    public long FollowupId { get; set; }
    public string? SuggestedSubject { get; set; }
    public string? SuggestedBody { get; set; }
    public string? Reasoning { get; set; }
    public string? WarningMessage { get; set; }
}

/// <summary>Request for POST /api/ea/followups/{followupId}/ai/reminder/send. The EA's
/// reviewed (or fully retyped) subject/body — never re-derived or re-calls Claude.</summary>
public class SendFollowupReminderRequestDto
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

/// <summary>Confirms a real SMTP send happened via IEaReminderEmailSender — this is an actual
/// delivery result, unlike FollowupEmailActionResponseDto's mailto: handoff.</summary>
public class FollowupAiReminderSentResponseDto
{
    public long FollowupId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}

// ============================================================
// Suggest escalation (suggest, then EA-confirmed apply via the real EscalationService)
// ============================================================

/// <summary>Response for POST /api/ea/followups/{followupId}/ai/suggest-escalation.</summary>
public class FollowupAiEscalationSuggestionResponseDto
{
    public long FollowupId { get; set; }
    /// <summary>Null when Claude judged escalation isn't warranted yet — never guessed.</summary>
    public int? RecommendedEscalationLevelId { get; set; }
    public string? RecommendedEscalationLevelName { get; set; }
    public string? Reasoning { get; set; }
    public string? WarningMessage { get; set; }
}

/// <summary>Request for POST /api/ea/followups/{followupId}/ai/suggest-escalation/apply.
/// Creates the real Escalation with the EA's reviewed (or overridden) level — never re-calls
/// Claude, only forwards to EscalationService.CreateAsync.</summary>
public class ApplySuggestedEscalationRequestDto
{
    public int EscalationLevelId { get; set; }
    public string? Notes { get; set; }
    public string? EscalatedToId { get; set; }
    public string? EscalatedToName { get; set; }
}

// ============================================================
// Predict resolution time (preview only — Followup has no field to write this into)
// ============================================================

/// <summary>Response for POST /api/ea/followups/{followupId}/ai/predict-resolution.</summary>
public class FollowupAiResolutionPredictionResponseDto
{
    public long FollowupId { get; set; }
    public DateTime? PredictedResolutionDate { get; set; }
    /// <summary>HistoricalAverage | None.</summary>
    public string Basis { get; set; } = string.Empty;
    public string? Explanation { get; set; }
    public string? WarningMessage { get; set; }
}

// ============================================================
// Detect at-risk items (preview only)
// ============================================================

/// <summary>Response for POST /api/ea/followups/{followupId}/ai/at-risk.</summary>
public class FollowupAiAtRiskResponseDto
{
    public long FollowupId { get; set; }
    /// <summary>Low | Medium | High.</summary>
    public string RiskLevel { get; set; } = string.Empty;
    public string? Reasoning { get; set; }
    public string? SuggestedAction { get; set; }
}
