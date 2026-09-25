using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// Preview-only AI assistance for Delegation (owner suggestion, due-date prediction, delay-
/// risk check). Orchestration only — provider-specific logic (Claude, JSON parsing) lives in
/// the already-registered shared services; this class never talks to Claude directly. Never
/// writes to Delegation/EaTask/WorkPause — every method here is read-only advisory output
/// for the EA to act on manually via the existing lifecycle/update endpoints.
/// </summary>
public class DelegationAiService : IDelegationAiService
{
    private readonly EaFmsDbContext _db;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDelegationAiPromptBuilder _promptBuilder;
    private readonly IDelegationService _delegations;
    private readonly ITatRuleRepository _tatRules;
    private readonly IDelegationAiRepository _repository;
    private readonly ILogger<DelegationAiService> _logger;
    private readonly int _maxAiAttempts;

    // IClaudeClient is resolved lazily via IServiceProvider (not a direct constructor
    // dependency) — same reason as MeetingAiService/TravelAiService/ApprovalAiService:
    // IClaudeClient is a singleton whose constructor throws if AnthropicSettings:ApiKey is
    // missing, and this service must not be forced to fail to construct in an environment
    // without a configured key.
    public DelegationAiService(
        EaFmsDbContext db,
        IServiceProvider serviceProvider,
        IDelegationAiPromptBuilder promptBuilder,
        IDelegationService delegations,
        ITatRuleRepository tatRules,
        IDelegationAiRepository repository,
        ILogger<DelegationAiService> logger,
        IOptions<ClaudeOptions> claudeOptions)
    {
        _db = db;
        _serviceProvider = serviceProvider;
        _promptBuilder = promptBuilder;
        _delegations = delegations;
        _tatRules = tatRules;
        _repository = repository;
        _logger = logger;
        _maxAiAttempts = Math.Clamp(claudeOptions.Value.MaxRetries, 1, 3);
    }

    public async Task<DelegationAiOwnerSuggestionResponseDto> SuggestOwnerAsync(long delegationId, CancellationToken ct = default)
    {
        var delegation = await _delegations.GetByIdAsync(delegationId, ct); // throws NotFoundException
        var response = await SuggestOwnerCoreAsync(delegation, delegationId, ct);
        await AiSuggestionWriters.LogDelegationOwnerSuggestionAsync(_db, delegationId, response, ct);
        return response;
    }

    // Draft variant for the New Delegation form — there is no delegation row yet, so nothing
    // is excluded from history (0 never matches a real id) and no suggestion log is written.
    public Task<DelegationAiOwnerSuggestionResponseDto> SuggestOwnerForDraftAsync(DelegationAiDraftRequestDto dto, CancellationToken ct = default)
        => SuggestOwnerCoreAsync(ToDraftDelegation(dto), 0, ct);

    private async Task<DelegationAiOwnerSuggestionResponseDto> SuggestOwnerCoreAsync(DelegationResponseDto delegation, long delegationId, CancellationToken ct)
    {
        const string warning = "Based on historical delegations of the same type only — not an org chart or assignment rule.";

        if (string.IsNullOrWhiteSpace(delegation.DelegationType))
        {
            var noTypeResponse = new DelegationAiOwnerSuggestionResponseDto
            {
                DelegationId = delegationId,
                SuggestedDoerId = null,
                HistoricalSampleSize = 0,
                Reasoning = "This delegation has no delegationType set, so there is no historical group to compare it against.",
                WarningMessage = warning,
            };
            return noTypeResponse;
        }

        // Historical stats computed here in C#, never invented by Claude — this system has
        // no employee/role directory, so the only honest source of a "who usually does this
        // kind of thing" signal is who actually did similar delegations before.
        var history = await _repository.GetTopDoersByTypeAsync(delegationId, delegation.DelegationType, ct);

        if (history.Count == 0)
        {
            var noHistoryResponse = new DelegationAiOwnerSuggestionResponseDto
            {
                DelegationId = delegationId,
                SuggestedDoerId = null,
                HistoricalSampleSize = 0,
                Reasoning = "No past delegations of this type were found yet — there is no history to base a suggestion on.",
                WarningMessage = warning,
            };
            return noHistoryResponse;
        }

        var candidates = history.Select(h => (h.DoerId, h.DoerName, h.Count)).ToList();
        var systemPrompt = _promptBuilder.BuildOwnerSystemPrompt();
        var userPrompt = _promptBuilder.BuildOwnerUserPrompt(delegation, candidates);

        var aiResult = await GenerateAndParseAsync<DelegationAiOwnerResultDto>(
            systemPrompt, userPrompt, "Delegation owner suggestion", ct);

        // Defensive: Claude may only ever pick a doerId we actually gave it. If it returns
        // something else (or malformed), fall back to the most-frequent real candidate
        // rather than surface a possibly-hallucinated identity.
        var match = candidates.FirstOrDefault(c => string.Equals(c.DoerId, aiResult.RecommendedDoerId, StringComparison.OrdinalIgnoreCase));
        var chosen = aiResult.RecommendedDoerId == null
            ? default
            : (match.DoerId != null ? match : candidates[0]);

        var response = new DelegationAiOwnerSuggestionResponseDto
        {
            DelegationId = delegationId,
            SuggestedDoerId = aiResult.RecommendedDoerId == null ? null : chosen.DoerId,
            SuggestedDoerName = aiResult.RecommendedDoerId == null ? null : chosen.DoerName,
            HistoricalSampleSize = history.Sum(h => h.Count),
            Reasoning = aiResult.Reasoning,
            WarningMessage = warning,
        };
        return response;
    }

    public async Task<DelegationResponseDto> ApplySuggestedOwnerAsync(long delegationId, ApplySuggestedOwnerRequestDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.DoerId))
            throw new BusinessRuleException("DoerId must not be empty.");

        var current = await _delegations.GetByIdAsync(delegationId, ct); // throws NotFoundException
        var update = ToUpdateDto(current);
        update.DoerId = dto.DoerId;
        update.DoerNameSnapshot = dto.DoerName;

        // Both writes run in one transaction — DelegationService.UpdateAsync is reentrant-safe
        // (joins this transaction instead of committing its own) — so the real owner change
        // and its suggestion-log entry are never left half-done relative to each other.
        // UpdateAsync itself enforces the "not once Completed" rule — not duplicated here.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var updated = await _delegations.UpdateAsync(delegationId, update, ct);
        await AiSuggestionWriters.MarkDelegationOwnerSuggestionAppliedAsync(_db, delegationId, dto, ct);
        await tx.CommitAsync(ct);
        await _serviceProvider.MarkAiResponseUsedAsync(EaAiModules.Delegation, ["Delegation owner suggestion"], delegationId,
            new { dto.DoerId, dto.DoerName }, ct);
        return updated;
    }

    public async Task<DelegationAiDueDatePredictionResponseDto> PredictDueDateAsync(long delegationId, CancellationToken ct = default)
    {
        var delegation = await _delegations.GetByIdAsync(delegationId, ct); // throws NotFoundException

        if (delegation.Status == DelegationStatus.Completed)
            throw new BusinessRuleException("Delegation is already Completed; there is nothing to predict a due date for.");

        var response = await PredictDueDateCoreAsync(delegation, delegationId, ct);
        await AiSuggestionWriters.LogDelegationDueDatePredictionAsync(_db, delegationId, response, ct);
        return response;
    }

    // Draft variant for the New Delegation form — anchored on the planned start date (or now),
    // nothing excluded from history, and no prediction log is written.
    public Task<DelegationAiDueDatePredictionResponseDto> PredictDueDateForDraftAsync(DelegationAiDraftRequestDto dto, CancellationToken ct = default)
        => PredictDueDateCoreAsync(ToDraftDelegation(dto), 0, ct);

    private async Task<DelegationAiDueDatePredictionResponseDto> PredictDueDateCoreAsync(DelegationResponseDto delegation, long delegationId, CancellationToken ct)
    {

        const string warning = "An estimate only, not a guarantee — based on a configured turnaround time or past similar delegations, never on the specific work involved.";
        var anchor = delegation.StartedAt ?? delegation.StartDate ?? delegation.CreatedAt;

        if (!string.IsNullOrWhiteSpace(delegation.DelegationType))
        {
            var moduleId = await ResolveDelegationModuleIdAsync(ct);
            if (moduleId.HasValue)
            {
                var rules = await _tatRules.GetApplicableByTypeOnlyAsync(moduleId.Value, delegation.DelegationType, DelegationTaskType.Actual, ct);
                if (rules.Count == 1)
                {
                    var suggested = anchor.AddMinutes(rules[0].TatMinutes);
                    var explanation = await ExplainDueDateAsync(delegation, "ConfiguredTat", suggested, ct);
                    var configuredResponse = new DelegationAiDueDatePredictionResponseDto
                    {
                        DelegationId = delegationId, SuggestedDueDate = suggested, Basis = "ConfiguredTat",
                        Explanation = explanation, WarningMessage = warning,
                    };
                    return configuredResponse;
                }
            }

            var samples = await _repository.GetCompletedDurationSamplesAsync(delegationId, delegation.DelegationType, ct);

            if (samples.Count > 0)
            {
                var avgMinutes = samples.Average(s => (s.CompletedAt - s.StartedAt).TotalMinutes);
                var suggested = anchor.AddMinutes(avgMinutes);
                var explanation = await ExplainDueDateAsync(delegation, "HistoricalAverage", suggested, ct);
                var historicalResponse = new DelegationAiDueDatePredictionResponseDto
                {
                    DelegationId = delegationId, SuggestedDueDate = suggested, Basis = "HistoricalAverage",
                    Explanation = explanation, WarningMessage = warning,
                };
                return historicalResponse;
            }
        }

        var noBasisResponse = new DelegationAiDueDatePredictionResponseDto
        {
            DelegationId = delegationId,
            SuggestedDueDate = null,
            Basis = "None",
            Explanation = "No configured turnaround time and no completed delegations of this type exist yet to estimate from.",
            WarningMessage = warning,
        };
        return noBasisResponse;
    }

    public async Task<DelegationResponseDto> ApplyPredictedDueDateAsync(long delegationId, ApplyPredictedDueDateRequestDto dto, CancellationToken ct = default)
    {
        var current = await _delegations.GetByIdAsync(delegationId, ct); // throws NotFoundException
        var update = ToUpdateDto(current);
        update.EndDate = dto.EndDate;

        // Both writes run in one transaction — see ApplySuggestedOwnerAsync for why.
        // UpdateAsync itself enforces the "not once Completed" rule — not duplicated here.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var updated = await _delegations.UpdateAsync(delegationId, update, ct);
        await AiSuggestionWriters.MarkDelegationDueDatePredictionAppliedAsync(_db, delegationId, dto, ct);
        await tx.CommitAsync(ct);
        await _serviceProvider.MarkAiResponseUsedAsync(EaAiModules.Delegation, ["Delegation due date explanation"], delegationId,
            new { dto.EndDate }, ct);
        return updated;
    }

    // Shapes the unsaved New Delegation form into the DTO the prompt builders already read.
    private static DelegationResponseDto ToDraftDelegation(DelegationAiDraftRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.DelegationType))
            throw new BusinessRuleException("Choose a delegation type first — suggestions are based on past delegations of the same type.");

        return new DelegationResponseDto
        {
            Title = dto.Title?.Trim() ?? string.Empty,
            Description = dto.Description,
            DelegationType = dto.DelegationType.Trim(),
            Priority = dto.Priority,
            SourceModuleName = dto.SourceModuleName,
            StartDate = dto.StartDate,
            CreatedAt = DateTime.Now,
            Status = DelegationStatus.Pending,
        };
    }

    // DelegationService.UpdateAsync is a full-replace endpoint (every editable field is
    // overwritten from the DTO, missing ones become blank) — so both Apply methods above
    // must first copy every current editable field unchanged before overriding the one the
    // EA actually confirmed, or they would silently wipe Title/Description/StartDate/etc.
    private static DelegationUpdateRequestDto ToUpdateDto(DelegationResponseDto d) => new()
    {
        Title = d.Title,
        Description = d.Description,
        DelegationType = d.DelegationType,
        DoerId = d.DoerId,
        DoerNameSnapshot = d.DoerName,
        AssigneeId = d.AssigneeId,
        AssigneeName = d.AssigneeName,
        StartDate = d.StartDate,
        EndDate = d.EndDate,
        Priority = d.Priority,
        SourceBusinessModuleId = d.SourceBusinessModuleId,
        SourceEntityId = d.SourceEntityId,
        SourceReference = d.SourceReference,
        AdditionalNotes = d.AdditionalNotes,
    };

    public async Task<DelegationAiDelayRiskResponseDto> CheckDelayRiskAsync(long delegationId, CancellationToken ct = default)
    {
        var delegation = await _delegations.GetByIdAsync(delegationId, ct); // throws NotFoundException

        if (delegation.Status == DelegationStatus.Completed)
            throw new BusinessRuleException("Delegation is already Completed; there is no delay risk to assess.");

        var systemPrompt = _promptBuilder.BuildDelayRiskSystemPrompt();
        var userPrompt = _promptBuilder.BuildDelayRiskUserPrompt(delegation);

        var aiResult = await GenerateAndParseAsync<DelegationAiDelayRiskResultDto>(
            systemPrompt, userPrompt, "Delegation delay risk check", ct);

        var riskLevel = new[] { "Low", "Medium", "High" }
            .FirstOrDefault(r => string.Equals(r, aiResult.RiskLevel?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? "Medium";

        var response = new DelegationAiDelayRiskResponseDto
        {
            DelegationId = delegationId,
            RiskLevel = riskLevel,
            Reasoning = aiResult.Reasoning,
            SuggestedNudgeMessage = string.IsNullOrWhiteSpace(aiResult.SuggestedNudgeMessage) ? null : aiResult.SuggestedNudgeMessage,
        };
        await AiSuggestionWriters.LogDelegationDelayRiskCheckAsync(_db, delegationId, response, ct);
        return response;
    }

    private async Task<string?> ExplainDueDateAsync(DelegationResponseDto delegation, string basis, DateTime suggested, CancellationToken ct)
    {
        var systemPrompt = _promptBuilder.BuildDueDateSystemPrompt();
        var userPrompt = _promptBuilder.BuildDueDateUserPrompt(delegation, basis, suggested);
        var aiResult = await GenerateAndParseAsync<DelegationAiDueDateResultDto>(
            systemPrompt, userPrompt, "Delegation due date explanation", ct);
        return aiResult.Explanation;
    }

    private Task<long?> ResolveDelegationModuleIdAsync(CancellationToken ct) => _repository.ResolveDelegationModuleIdAsync(ct);

    // Same retry-on-malformed-JSON pattern as MeetingAiService/TravelAiService/ApprovalAiService.
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
            // Every EA AI call is recorded in ea_ai_usage_logs (who, where, for what, prompt, response, tokens).
            var aiUsage = _serviceProvider.GetService<IEaAiUsageLogger>();
            var rawResponse = aiUsage is null
                ? await claudeClient.GenerateJsonAsync(systemPrompt, attemptPrompt, ct)
                : await aiUsage.CallAsync(EaAiModules.Delegation, entityName, systemPrompt, attemptPrompt, attempt,
                    () => claudeClient.GenerateJsonAsync(systemPrompt, attemptPrompt, ct), ct);

            try
            {
                return AiJsonResponseParser.Parse<T>(rawResponse, _logger, entityName);
            }
            catch (BusinessRuleException ex)
            {
                if (aiUsage is not null) await aiUsage.MarkLastInvalidJsonAsync(ex.Message);
                if (attempt >= _maxAiAttempts) throw;
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
internal class DelegationAiOwnerResultDto
{
    public string? RecommendedDoerId { get; set; }
    public string? Reasoning { get; set; }
}

internal class DelegationAiDueDateResultDto
{
    public string? Explanation { get; set; }
}

internal class DelegationAiDelayRiskResultDto
{
    public string? RiskLevel { get; set; }
    public string? Reasoning { get; set; }
    public string? SuggestedNudgeMessage { get; set; }
}
