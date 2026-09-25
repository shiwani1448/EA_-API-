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
/// Preview-only AI assistance for Approval Management (readiness check, approver
/// suggestion, status summary). Orchestration only — provider-specific logic (Claude, JSON
/// parsing) lives in the already-registered shared services; this class never talks to
/// Claude directly. Never writes to WorkflowStatus/ApprovalCycle/documents — every method
/// here is read-only advisory output for the EA to act on manually via the existing
/// lifecycle/document endpoints.
/// </summary>
public class ApprovalAiService : IApprovalAiService
{
    private readonly EaFmsDbContext _db;
    private readonly IServiceProvider _serviceProvider;
    private readonly IApprovalAiPromptBuilder _promptBuilder;
    private readonly ApprovalQueryService _queries;
    private readonly ApprovalService _approvals;
    private readonly IApprovalAiRepository _repository;
    private readonly ILogger<ApprovalAiService> _logger;
    private readonly int _maxAiAttempts;

    // IClaudeClient is resolved lazily via IServiceProvider (not a direct constructor
    // dependency) — same reason as MeetingAiService/TravelAiService: IClaudeClient is a
    // singleton whose constructor throws if AnthropicSettings:ApiKey is missing, and this
    // service must not be forced to fail to construct in an environment without a
    // configured key.
    public ApprovalAiService(
        EaFmsDbContext db,
        IServiceProvider serviceProvider,
        IApprovalAiPromptBuilder promptBuilder,
        ApprovalQueryService queries,
        ApprovalService approvals,
        IApprovalAiRepository repository,
        ILogger<ApprovalAiService> logger,
        IOptions<ClaudeOptions> claudeOptions)
    {
        _db = db;
        _serviceProvider = serviceProvider;
        _promptBuilder = promptBuilder;
        _queries = queries;
        _approvals = approvals;
        _repository = repository;
        _logger = logger;
        _maxAiAttempts = Math.Clamp(claudeOptions.Value.MaxRetries, 1, 3);
    }

    private async Task<ApprovalDetailDto> LoadAsync(long approvalRequestId, CancellationToken ct) =>
        await _queries.DetailAsync(approvalRequestId, ct)
        ?? throw new NotFoundException($"Approval request {approvalRequestId} not found.");

    public async Task<ApprovalAiReadinessResponseDto> CheckReadinessAsync(long approvalRequestId, CancellationToken ct = default)
    {
        var detail = await LoadAsync(approvalRequestId, ct);

        return await CheckReadinessAsync(new ApprovalAiReadinessInput
        {
            SavedRequestId = approvalRequestId,
            RequestTitle = detail.RequestTitle, RequestType = detail.Type,
            Priority = detail.Priority, Department = detail.Department,
            Description = detail.Description, Justification = detail.Justification,
            Amount = detail.Amount, Currency = detail.Currency,
            RequiredApprovalDate = detail.RequiredApprovalDate, ApproverName = detail.Approver,
            DocumentFileNames = detail.Documents.Select(d => d.OriginalFileName).ToList(),
            WorkflowStatus = detail.WorkflowStatus, CurrentCycleNo = detail.CurrentCycleNo,
            LatestCycle = detail.LatestCycle,
        }, ct);
    }

    public async Task<ApprovalAiReadinessResponseDto> CheckReadinessAsync(ApprovalAiReadinessInput detail, CancellationToken ct = default)
    {
        var approvalRequestId = detail.SavedRequestId;
        var systemPrompt = _promptBuilder.BuildReadinessSystemPrompt();
        var userPrompt = _promptBuilder.BuildReadinessUserPrompt(detail);

        var aiResult = await GenerateAndParseAsync<ApprovalAiReadinessResultDto>(
            systemPrompt, userPrompt, "Approval readiness check", ct);

        var response = new ApprovalAiReadinessResponseDto
        {
            ApprovalRequestId = approvalRequestId,
            IsLikelyReady = aiResult.IsLikelyReady,
            MissingFields = (aiResult.MissingFields ?? new List<string>()).Where(f => !string.IsNullOrWhiteSpace(f)).ToList(),
            SuggestedDocuments = (aiResult.SuggestedDocuments ?? new List<string>()).Where(d => !string.IsNullOrWhiteSpace(d)).ToList(),
            Notes = aiResult.Notes,
            WarningMessage = "Based only on request field values and uploaded file names — AI cannot read the contents of any uploaded document.",
        };
        if (approvalRequestId.HasValue)
            await AiSuggestionWriters.LogApprovalReadinessCheckAsync(_db, approvalRequestId.Value, response, ct);
        return response;
    }

    public async Task<ApprovalAiApproverSuggestionResponseDto> RecommendApproverAsync(long approvalRequestId, CancellationToken ct = default)
    {
        var detail = await LoadAsync(approvalRequestId, ct);
        return await RecommendApproverAsync(new ApprovalAiApproverInput
        {
            SavedRequestId = approvalRequestId, RequestType = detail.Type,
            Department = detail.Department, Amount = detail.Amount,
            Currency = detail.Currency, SavedPriority = detail.Priority,
        }, ct);
    }

    public async Task<ApprovalAiApproverSuggestionResponseDto> RecommendApproverAsync(ApprovalAiApproverInput detail, CancellationToken ct = default)
    {
        var approvalRequestId = detail.SavedRequestId;
        const string warning = "Based on historical approval patterns in this department only — not an org chart or authorization rule.";

        if (string.IsNullOrWhiteSpace(detail.Department))
        {
            var noDepartmentResponse = new ApprovalAiApproverSuggestionResponseDto
            {
                ApprovalRequestId = approvalRequestId,
                RecommendedApproverName = null,
                HistoricalSampleSize = 0,
                Reasoning = "This request has no department set, so there is no historical group to compare it against.",
                WarningMessage = warning,
            };
            if (approvalRequestId.HasValue)
                await AiSuggestionWriters.LogApprovalApproverRecommendationAsync(_db, approvalRequestId.Value, noDepartmentResponse, ct);
            return noDepartmentResponse;
        }

        // Historical stats computed here in C#, never invented by Claude — this system has
        // no employee/role directory, so the only honest source of a "who approves this
        // kind of thing" signal is who actually approved similar requests before.
        var history = await _db.ApprovalRequests.AsNoTracking()
            .Where(a => !a.IsDeleted && a.Id != approvalRequestId && a.Department == detail.Department
                && a.WorkflowStatus == "Approved" && a.ApprovedBy != null)
            .GroupBy(a => a.ApprovedBy!)
            .Select(g => new { Approver = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(5)
            .ToListAsync(ct);

        if (history.Count == 0)
        {
            var noHistoryResponse = new ApprovalAiApproverSuggestionResponseDto
            {
                ApprovalRequestId = approvalRequestId,
                RecommendedApproverName = null,
                HistoricalSampleSize = 0,
                Reasoning = "No approved requests were found for this department yet — there is no history to base a recommendation on.",
                WarningMessage = warning,
            };
            if (approvalRequestId.HasValue)
                await AiSuggestionWriters.LogApprovalApproverRecommendationAsync(_db, approvalRequestId.Value, noHistoryResponse, ct);
            return noHistoryResponse;
        }

        var candidates = history.Select(h => (h.Approver, h.Count)).ToList();
        var systemPrompt = _promptBuilder.BuildApproverSystemPrompt();
        var userPrompt = _promptBuilder.BuildApproverUserPrompt(detail, candidates);

        var aiResult = await GenerateAndParseAsync<ApprovalAiApproverResultDto>(
            systemPrompt, userPrompt, "Approval approver suggestion", ct);

        // Defensive: Claude may only ever pick a name we actually gave it. If it returns
        // something else (or malformed), fall back to the most-frequent real candidate
        // rather than surface a possibly-hallucinated name.
        var recommended = aiResult.RecommendedApprover != null
            && candidates.Any(c => string.Equals(c.Approver, aiResult.RecommendedApprover, StringComparison.OrdinalIgnoreCase))
            ? aiResult.RecommendedApprover
            : (aiResult.RecommendedApprover == null ? null : candidates[0].Approver);

        var response = new ApprovalAiApproverSuggestionResponseDto
        {
            ApprovalRequestId = approvalRequestId,
            RecommendedApproverName = recommended,
            HistoricalSampleSize = history.Sum(h => h.Count),
            Reasoning = aiResult.Reasoning,
            WarningMessage = warning,
        };
        if (approvalRequestId.HasValue)
            await AiSuggestionWriters.LogApprovalApproverRecommendationAsync(_db, approvalRequestId.Value, response, ct);
        return response;
    }

    public async Task<ApprovalDetailDto> ApplyRecommendedApproverAsync(long approvalRequestId, ApplyApproverSuggestionRequestDto dto, CancellationToken ct = default)
    {
        // Writes exactly the value the caller sent — never re-derives or re-calls Claude, so
        // this is safe to call whether the EA accepted the AI's exact suggestion or typed
        // their own choice instead. ApprovalService.SetApproverAsync owns the actual
        // status-gate (PendingApproval/ChangesRequested only) and audit entry. Both writes
        // run in one transaction so the real approver change and its suggestion-log entry
        // are never left half-done relative to each other.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _approvals.SetApproverAsync(approvalRequestId, dto.ApproverId, dto.ApproverName, ct);
        await AiSuggestionWriters.MarkApprovalApproverRecommendationAppliedAsync(_db, approvalRequestId, dto, ct);
        await tx.CommitAsync(ct);
        await _serviceProvider.MarkAiResponseUsedAsync(EaAiModules.Approval, ["Approval approver suggestion"], approvalRequestId,
            new { dto.ApproverId, dto.ApproverName }, ct);

        return await LoadAsync(approvalRequestId, ct);
    }

    public async Task<ApprovalAiStatusSummaryResponseDto> SummarizeStatusAsync(long approvalRequestId, CancellationToken ct = default)
    {
        var detail = await LoadAsync(approvalRequestId, ct);

        var systemPrompt = _promptBuilder.BuildStatusSystemPrompt();
        var userPrompt = _promptBuilder.BuildStatusUserPrompt(detail);

        var aiResult = await GenerateAndParseAsync<ApprovalAiStatusResultDto>(
            systemPrompt, userPrompt, "Approval status summary", ct);

        var response = new ApprovalAiStatusSummaryResponseDto
        {
            ApprovalRequestId = approvalRequestId,
            Summary = aiResult.Summary,
            WorkflowStatus = detail.WorkflowStatus,
            CurrentCycleNo = detail.CurrentCycleNo,
            DueState = detail.DueState,
        };
        await AiSuggestionWriters.LogApprovalStatusSummaryAsync(_db, approvalRequestId, response, ct);
        return response;
    }

    // Same retry-on-malformed-JSON pattern as MeetingAiService/TravelAiService.
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
                : await aiUsage.CallAsync(EaAiModules.Approval, entityName, systemPrompt, attemptPrompt, attempt,
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
internal class ApprovalAiReadinessResultDto
{
    public bool IsLikelyReady { get; set; }
    public List<string> MissingFields { get; set; } = new();
    public List<string> SuggestedDocuments { get; set; } = new();
    public string? Notes { get; set; }
}

internal class ApprovalAiApproverResultDto
{
    public string? RecommendedApprover { get; set; }
    public string? Reasoning { get; set; }
}

internal class ApprovalAiStatusResultDto
{
    public string? Summary { get; set; }
}
