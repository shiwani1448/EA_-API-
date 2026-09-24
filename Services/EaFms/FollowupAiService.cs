using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// Preview-only AI assistance for Follow-up &amp; Escalation (reminder draft, escalation
/// suggestion, resolution-time prediction, at-risk check). Orchestration only —
/// provider-specific logic (Claude, JSON parsing) lives in the already-registered shared
/// services; this class never talks to Claude directly.
/// </summary>
public class FollowupAiService : IFollowupAiService
{
    private static readonly string[] RiskLevels = { "Low", "Medium", "High" };

    private readonly EaFmsDbContext _db;
    private readonly IServiceProvider _serviceProvider;
    private readonly IFollowupAiPromptBuilder _promptBuilder;
    private readonly IFollowupService _followups;
    private readonly IEscalationService _escalations;
    private readonly IFollowupCycleRepository _cycles;
    private readonly IFollowupAiRepository _repository;
    private readonly IEaReminderEmailSender _reminderSender;
    private readonly ILogger<FollowupAiService> _logger;
    private readonly int _maxAiAttempts;

    // IClaudeClient is resolved lazily via IServiceProvider (not a direct constructor
    // dependency) — same reason as every other AI service: IClaudeClient is a singleton
    // whose constructor throws if AnthropicSettings:ApiKey is missing, and this service must
    // not be forced to fail to construct in an environment without a configured key.
    public FollowupAiService(
        EaFmsDbContext db,
        IServiceProvider serviceProvider,
        IFollowupAiPromptBuilder promptBuilder,
        IFollowupService followups,
        IEscalationService escalations,
        IFollowupCycleRepository cycles,
        IFollowupAiRepository repository,
        IEaReminderEmailSender reminderSender,
        ILogger<FollowupAiService> logger,
        IOptions<ClaudeOptions> claudeOptions)
    {
        _db = db;
        _serviceProvider = serviceProvider;
        _promptBuilder = promptBuilder;
        _followups = followups;
        _escalations = escalations;
        _cycles = cycles;
        _repository = repository;
        _reminderSender = reminderSender;
        _logger = logger;
        _maxAiAttempts = Math.Clamp(claudeOptions.Value.MaxRetries, 1, 3);
    }

    // ============================================================
    // Auto reminders
    // ============================================================

    public async Task<FollowupAiReminderSuggestionResponseDto> SuggestReminderAsync(long followupId, CancellationToken ct = default)
    {
        var followup = await _followups.GetByIdAsync(followupId, ct); // throws NotFoundException

        var systemPrompt = _promptBuilder.BuildReminderSystemPrompt();
        var userPrompt = _promptBuilder.BuildReminderUserPrompt(followup);
        var aiResult = await GenerateAndParseAsync<FollowupAiReminderResultDto>(systemPrompt, userPrompt, "Followup reminder draft", ct);

        var response = new FollowupAiReminderSuggestionResponseDto
        {
            FollowupId = followupId,
            SuggestedSubject = string.IsNullOrWhiteSpace(aiResult.Subject) ? null : aiResult.Subject.Trim(),
            SuggestedBody = string.IsNullOrWhiteSpace(aiResult.Body) ? null : aiResult.Body.Trim(),
            Reasoning = aiResult.Reasoning,
            WarningMessage = "A draft only — review the wording before sending; nothing is sent yet.",
        };
        await AiSuggestionWriters.LogFollowupReminderSuggestionAsync(_db, followupId, response, ct);
        return response;
    }

    public async Task<FollowupAiReminderSentResponseDto> SendSuggestedReminderAsync(long followupId, SendFollowupReminderRequestDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Subject) || string.IsNullOrWhiteSpace(dto.Body))
            throw new BusinessRuleException("Subject and body must not be empty.");

        var followup = await _followups.GetByIdAsync(followupId, ct); // throws NotFoundException
        if (string.IsNullOrWhiteSpace(followup.ReminderRecipientEmail))
            throw new BusinessRuleException("This followup has no reminder recipient email configured.");

        // Writes exactly the subject/body the caller sent — never re-derives or re-calls
        // Claude, so this is safe whether the EA accepted the AI's exact draft or rewrote it
        // herself. IEaReminderEmailSender is the real, pre-existing SMTP sender — this is the
        // first caller that actually invokes it for a Followup reminder.
        var sentAt = Clock.UtcNowTz;
        await _reminderSender.SendAsync(new EaReminderEmailMessage(followup.ReminderRecipientEmail, dto.Subject.Trim(), dto.Body.Trim()), ct);
        await AiSuggestionWriters.MarkFollowupReminderSuggestionAppliedAsync(_db, followupId, followup.ReminderRecipientEmail, ct);

        return new FollowupAiReminderSentResponseDto
        {
            FollowupId = followupId,
            RecipientEmail = followup.ReminderRecipientEmail,
            Subject = dto.Subject.Trim(),
            SentAt = sentAt,
        };
    }

    // ============================================================
    // Suggest escalation
    // ============================================================

    public async Task<FollowupAiEscalationSuggestionResponseDto> SuggestEscalationAsync(long followupId, CancellationToken ct = default)
    {
        var followup = await _followups.GetByIdAsync(followupId, ct); // throws NotFoundException
        const string warning = "Based only on this follow-up's own overdue status, attempt count, and current escalation state — never an authorization rule.";

        var (cycleCount, currentLevel, _) = await LoadEscalationContextAsync(followupId, ct);

        var allLevels = await _escalations.GetActiveLevelsAsync(ct);
        var candidates = allLevels
            .Where(l => currentLevel == null || l.Level > currentLevel.Value)
            .OrderBy(l => l.Level)
            .Select(l => (l.Id, l.Code, l.Name, l.Level))
            .ToList();

        if (candidates.Count == 0)
        {
            var noneResponse = new FollowupAiEscalationSuggestionResponseDto
            {
                FollowupId = followupId,
                RecommendedEscalationLevelId = null,
                Reasoning = allLevels.Count == 0
                    ? "No escalation levels are configured yet."
                    : "This follow-up is already at the highest configured escalation level.",
                WarningMessage = warning,
            };
            await AiSuggestionWriters.LogFollowupEscalationSuggestionAsync(_db, followupId, noneResponse, ct);
            return noneResponse;
        }

        var systemPrompt = _promptBuilder.BuildEscalationSystemPrompt();
        var userPrompt = _promptBuilder.BuildEscalationUserPrompt(followup, candidates, cycleCount, currentLevel);
        var aiResult = await GenerateAndParseAsync<FollowupAiEscalationResultDto>(systemPrompt, userPrompt, "Followup escalation suggestion", ct);

        // Defensive: Claude may only ever pick a level we actually gave it as a candidate.
        // match.Code (a string) is used as the "found" signal rather than match.Id, since a
        // default(int) of 0 would be ambiguous with a genuine level id.
        var match = candidates.FirstOrDefault(c => string.Equals(c.Code, aiResult.RecommendedLevelCode, StringComparison.OrdinalIgnoreCase));
        var found = match.Code is not null;
        var response = new FollowupAiEscalationSuggestionResponseDto
        {
            FollowupId = followupId,
            RecommendedEscalationLevelId = found ? match.Id : null,
            RecommendedEscalationLevelName = found ? match.Name : null,
            Reasoning = aiResult.Reasoning,
            WarningMessage = warning,
        };
        await AiSuggestionWriters.LogFollowupEscalationSuggestionAsync(_db, followupId, response, ct);
        return response;
    }

    public async Task<EscalationResponseDto> ApplySuggestedEscalationAsync(long followupId, ApplySuggestedEscalationRequestDto dto, CancellationToken ct = default)
    {
        // Both writes run in one transaction — the real Escalation and its suggestion-log
        // entry are never left half-done relative to each other. EscalationService.CreateAsync
        // owns the actual validation (Followup/level exist, next-level ordering).
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var created = await _escalations.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followupId,
            EscalationLevelId = dto.EscalationLevelId,
            Notes = dto.Notes,
            EscalatedToId = dto.EscalatedToId,
            EscalatedToName = dto.EscalatedToName,
        }, ct);
        await AiSuggestionWriters.MarkFollowupEscalationSuggestionAppliedAsync(_db, followupId, created.Id, ct);
        await tx.CommitAsync(ct);
        return created;
    }

    // ============================================================
    // Predict resolution time
    // ============================================================

    public async Task<FollowupAiResolutionPredictionResponseDto> PredictResolutionTimeAsync(long followupId, CancellationToken ct = default)
    {
        var followup = await _followups.GetByIdAsync(followupId, ct); // throws NotFoundException

        if (followup.IsCompleted)
            throw new BusinessRuleException("Followup is already completed; there is nothing to predict a resolution time for.");

        const string warning = "An estimate only, not a guarantee — based on past follow-ups of the same type, never on the specific matter involved.";

        if (!string.IsNullOrWhiteSpace(followup.Type))
        {
            var samples = await _repository.GetCompletedDurationSamplesByTypeAsync(followupId, followup.Type, ct);
            if (samples.Count > 0)
            {
                var avgMinutes = samples.Average(s => (s.CompletedAt - s.CreatedDate).TotalMinutes);
                var suggested = followup.CreatedDate.AddMinutes(avgMinutes);
                var explanation = await ExplainResolutionAsync(followup, "HistoricalAverage", suggested, ct);
                var historicalResponse = new FollowupAiResolutionPredictionResponseDto
                {
                    FollowupId = followupId, PredictedResolutionDate = suggested, Basis = "HistoricalAverage",
                    Explanation = explanation, WarningMessage = warning,
                };
                await AiSuggestionWriters.LogFollowupResolutionPredictionAsync(_db, followupId, historicalResponse, ct);
                return historicalResponse;
            }
        }

        var noBasisResponse = new FollowupAiResolutionPredictionResponseDto
        {
            FollowupId = followupId,
            PredictedResolutionDate = null,
            Basis = "None",
            Explanation = "No completed follow-ups of this type exist yet to estimate from.",
            WarningMessage = warning,
        };
        await AiSuggestionWriters.LogFollowupResolutionPredictionAsync(_db, followupId, noBasisResponse, ct);
        return noBasisResponse;
    }

    private async Task<string?> ExplainResolutionAsync(FollowupResponseDto followup, string basis, DateTime suggested, CancellationToken ct)
    {
        var systemPrompt = _promptBuilder.BuildResolutionExplanationSystemPrompt();
        var userPrompt = _promptBuilder.BuildResolutionExplanationUserPrompt(followup, basis, suggested);
        var aiResult = await GenerateAndParseAsync<FollowupAiResolutionExplanationResultDto>(systemPrompt, userPrompt, "Followup resolution explanation", ct);
        return aiResult.Explanation;
    }

    // ============================================================
    // Detect at-risk items
    // ============================================================

    public async Task<FollowupAiAtRiskResponseDto> CheckAtRiskAsync(long followupId, CancellationToken ct = default)
    {
        var followup = await _followups.GetByIdAsync(followupId, ct); // throws NotFoundException

        if (followup.IsCompleted)
            throw new BusinessRuleException("Followup is already completed; there is no risk to assess.");

        var (cycleCount, currentLevel, hasUnresolvedEscalation) = await LoadEscalationContextAsync(followupId, ct);

        var systemPrompt = _promptBuilder.BuildAtRiskSystemPrompt();
        var userPrompt = _promptBuilder.BuildAtRiskUserPrompt(followup, cycleCount, currentLevel, hasUnresolvedEscalation);
        var aiResult = await GenerateAndParseAsync<FollowupAiAtRiskResultDto>(systemPrompt, userPrompt, "Followup at-risk check", ct);

        var riskLevel = RiskLevels.FirstOrDefault(r => string.Equals(r, aiResult.RiskLevel?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "Medium";

        var response = new FollowupAiAtRiskResponseDto
        {
            FollowupId = followupId,
            RiskLevel = riskLevel,
            Reasoning = aiResult.Reasoning,
            SuggestedAction = string.IsNullOrWhiteSpace(aiResult.SuggestedAction) ? null : aiResult.SuggestedAction,
        };
        await AiSuggestionWriters.LogFollowupAtRiskCheckAsync(_db, followupId, response, ct);
        return response;
    }

    private async Task<(int CycleCount, int? CurrentEscalationLevel, bool HasUnresolvedEscalation)> LoadEscalationContextAsync(long followupId, CancellationToken ct)
    {
        var cycleCount = (await _cycles.GetHistoryAsync(followupId, ct)).Count;
        var escalations = await _escalations.GetByFollowupIdAsync(followupId, ct);
        var latest = escalations.OrderByDescending(e => e.InitiatedAt).ThenByDescending(e => e.Id).FirstOrDefault();
        return (cycleCount, latest?.EscalationLevelNumber, latest is { IsResolved: false });
    }

    // Same retry-on-malformed-JSON pattern as every other AI service.
    private async Task<T> GenerateAndParseAsync<T>(
        string systemPrompt,
        string userPrompt,
        string entityName,
        CancellationToken ct) where T : new()
    {
        BusinessRuleException? lastParseError = null;

        for (var attempt = 1; attempt <= _maxAiAttempts; attempt++)
        {
            var attemptPrompt = attempt == 1
                ? userPrompt
                : userPrompt + """

                    IMPORTANT RETRY: The prior response was not valid JSON. Return one complete, compact JSON
                    object only. Do not use markdown fences, comments, smart quotes, trailing commas, or text
                    before/after the object.
                    """;

            var claudeClient = _serviceProvider.GetRequiredService<IClaudeClient>();
            var rawResponse = await claudeClient.GenerateJsonAsync(systemPrompt, attemptPrompt, ct);

            try
            {
                return AiJsonResponseParser.Parse<T>(rawResponse, _logger, entityName);
            }
            catch (BusinessRuleException ex) when (attempt < _maxAiAttempts)
            {
                lastParseError = ex;
                _logger.LogWarning(
                    "AI {Entity} returned invalid JSON on attempt {Attempt}/{MaxAttempts}; retrying.",
                    entityName, attempt, _maxAiAttempts);
            }
        }

        throw lastParseError
            ?? new BusinessRuleException($"AI {entityName} service did not return valid JSON.");
    }
}

// Internal shapes for parsing Claude's raw JSON only — never returned from the public API.
internal class FollowupAiReminderResultDto
{
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public string? Reasoning { get; set; }
}

internal class FollowupAiEscalationResultDto
{
    public string? RecommendedLevelCode { get; set; }
    public string? Reasoning { get; set; }
}

internal class FollowupAiResolutionExplanationResultDto
{
    public string? Explanation { get; set; }
}

internal class FollowupAiAtRiskResultDto
{
    public string? RiskLevel { get; set; }
    public string? Reasoning { get; set; }
    public string? SuggestedAction { get; set; }
}
