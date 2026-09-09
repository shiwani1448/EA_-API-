using Jarvis5.Common;
using Jarvis5.Dtos.Analysis;
using Jarvis5.Dtos.SolutionDesign;
using Jarvis5.Entities;
using Jarvis5.Repositories;
using Jarvis5.Services.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jarvis5.Services;

public class AnalysisService : IAnalysisService
{
    private readonly IRequestRepository _requestRepository;
    private readonly IAnalysisRepository _analysisRepository;
    private readonly ISolutionDesignRepository _solutionDesignRepository;
    private readonly IApprovalRepository _approvalRepository;
    private readonly IRequestHistoryRepository _historyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISimilarRequestFinder _similarRequestFinder;
    private readonly ISolutionKnowledgeFinder _knowledgeFinder;
    private readonly IAnalysisPromptBuilder _promptBuilder;
    private readonly IAnalysisReworkPromptBuilder _reworkPromptBuilder;
    private readonly IClaudeClient _claudeClient;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AnalysisService> _logger;
    private readonly string _modelName;
    private readonly int _maxAiAttempts;

    public AnalysisService(
        IRequestRepository requestRepository,
        IAnalysisRepository analysisRepository,
        ISolutionDesignRepository solutionDesignRepository,
        IApprovalRepository approvalRepository,
        IRequestHistoryRepository historyRepository,
        IUnitOfWork unitOfWork,
        ISimilarRequestFinder similarRequestFinder,
        ISolutionKnowledgeFinder knowledgeFinder,
        IAnalysisPromptBuilder promptBuilder,
        IAnalysisReworkPromptBuilder reworkPromptBuilder,
        IClaudeClient claudeClient,
        ICurrentUserService currentUser,
        ILogger<AnalysisService> logger,
        IOptions<ClaudeOptions> claudeOptions)
    {
        _requestRepository = requestRepository;
        _analysisRepository = analysisRepository;
        _solutionDesignRepository = solutionDesignRepository;
        _approvalRepository = approvalRepository;
        _historyRepository = historyRepository;
        _unitOfWork = unitOfWork;
        _similarRequestFinder = similarRequestFinder;
        _knowledgeFinder = knowledgeFinder;
        _promptBuilder = promptBuilder;
        _reworkPromptBuilder = reworkPromptBuilder;
        _claudeClient = claudeClient;
        _currentUser = currentUser;
        _logger = logger;
        _modelName = claudeOptions.Value.Model;
        _maxAiAttempts = Math.Clamp(claudeOptions.Value.MaxRetries, 1, 3);
    }

    public async Task<AnalysisDetailDto> GenerateAsync(long requestId, CancellationToken ct = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException($"Request {requestId} was not found.");

        if (request.CurrentStage != SCIHStage.RequestRaised && request.CurrentStage != SCIHStage.Analysis)
        {
            throw new BusinessRuleException(
                $"This request is already at stage '{SCIHStage.NameOf(request.CurrentStage)}'; the analysis can no longer be regenerated.");
        }

        var similarRequests = await _similarRequestFinder.FindTopSimilarAsync(request, topN: 5, ct: ct);

        var systemPrompt = _promptBuilder.BuildSystemPrompt();
        var userPrompt = _promptBuilder.BuildUserPrompt(request, similarRequests);

        var result = await GenerateAndParseAsync<AiAnalysisResultDto>(systemPrompt, userPrompt, "analysis", ct);

        var now = Clock.UtcNow;
        var newJson = JsonHelper.Serialize(result);

        var existing = await _analysisRepository.GetByRequestIdAsync(requestId, ct);
        SCIHAnalysis analysis;
        SCIHRequestHistory history;

        if (existing is null)
        {
            analysis = new SCIHAnalysis
            {
                RequestId = requestId,
                AnalysisJson = newJson,
                GeneratedByModel = _modelName,
                Status = SCIHAnalysisStatus.Completed,
                CreatedBy = _currentUser.UserId,
                CreatedDate = now
            };

            history = NewHistory(requestId, SCIHHistoryAction.AnalysisGenerated,
                "AI analysis generated.", previousValue: null, newValue: newJson, now);
        }
        else
        {
            analysis = existing;
            history = NewHistory(requestId, SCIHHistoryAction.AnalysisGenerated,
                "AI analysis regenerated.", previousValue: existing.AnalysisJson, newValue: newJson, now);

            analysis.AnalysisJson = newJson;
            analysis.GeneratedByModel = _modelName;
            analysis.Status = SCIHAnalysisStatus.Completed;
            analysis.ModifiedBy = _currentUser.UserId;
            analysis.ModifiedDate = now;
        }

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            if (existing is null)
                await _analysisRepository.AddAsync(analysis, ct);
            else
                _analysisRepository.Update(analysis);

            await _historyRepository.AddAsync(history, ct);

            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return ToDetailDto(analysis, request, result, "Analysis generated successfully.");
    }

    public async Task<AnalysisDetailDto> SaveAsync(long requestId, SaveAnalysisDto dto, CancellationToken ct = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException($"Request {requestId} was not found.");

        if (request.CurrentStage != SCIHStage.RequestRaised && request.CurrentStage != SCIHStage.Analysis)
        {
            throw new BusinessRuleException(
                $"This request is already at stage '{SCIHStage.NameOf(request.CurrentStage)}'; the analysis can no longer be edited.");
        }

        var now = Clock.UtcNow;
        var newJson = JsonHelper.Serialize(dto.Analysis);
        var isFirstSave = request.CurrentStage == SCIHStage.RequestRaised;

        var existing = await _analysisRepository.GetByRequestIdAsync(requestId, ct);
        SCIHAnalysis analysis;

        var histories = new List<SCIHRequestHistory>();

        if (existing is null)
        {
            analysis = new SCIHAnalysis
            {
                RequestId = requestId,
                AnalysisJson = newJson,
                GeneratedByModel = _modelName,
                CreatedBy = _currentUser.UserId,
                CreatedDate = now
            };

            histories.Add(NewHistory(requestId, SCIHHistoryAction.AnalysisSaved,
                "AI analysis reviewed and saved.", previousValue: null, newValue: newJson, now));
        }
        else
        {
            analysis = existing;
            histories.Add(NewHistory(requestId, SCIHHistoryAction.AnalysisUpdated,
                "Analysis updated.", previousValue: existing.AnalysisJson, newValue: newJson, now));

            analysis.AnalysisJson = newJson;
            analysis.GeneratedByModel = _modelName;
            analysis.ModifiedBy = _currentUser.UserId;
            analysis.ModifiedDate = now;
        }

        if (isFirstSave)
        {
            var previousStage = request.CurrentStage;
            request.CurrentStage = SCIHStage.Analysis;
            request.Status = SCIHStatus.Analysis;
            request.ModifiedBy = _currentUser.UserId;
            request.ModifiedDate = now;

            histories.Add(NewHistory(requestId, SCIHHistoryAction.StageChanged,
                $"Stage changed from '{SCIHStage.NameOf(previousStage)}' to '{SCIHStage.NameOf(SCIHStage.Analysis)}'.",
                JsonHelper.Serialize(new { stage = previousStage, stageName = SCIHStage.NameOf(previousStage) }),
                JsonHelper.Serialize(new { stage = SCIHStage.Analysis, stageName = SCIHStage.NameOf(SCIHStage.Analysis) }),
                now));
        }

        if (histories.Count > 0 && !string.IsNullOrWhiteSpace(dto.Remarks))
            histories[^1].Remarks = dto.Remarks;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            if (existing is null)
                await _analysisRepository.AddAsync(analysis, ct);
            else
                _analysisRepository.Update(analysis);

            if (isFirstSave)
                _requestRepository.Update(request);

            foreach (var history in histories)
                await _historyRepository.AddAsync(history, ct);

            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return ToDetailDto(analysis, request, dto.Analysis, "Analysis saved successfully.");
    }

    public async Task<AnalysisDetailDto> GetByRequestIdAsync(long requestId, CancellationToken ct = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException($"Request {requestId} was not found.");

        var analysis = await _analysisRepository.GetByRequestIdAsync(requestId, ct)
            ?? throw new NotFoundException($"No analysis has been saved yet for request {requestId}.");

        return ToDetailDto(analysis, request, JsonHelper.DeserializeObjectOrDefault<AiAnalysisResultDto>(analysis.AnalysisJson), "Analysis retrieved successfully.");
    }

    public async Task<AnalysisDetailDto> ReworkAsync(long requestId, CancellationToken ct = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException($"Request {requestId} was not found.");

        if (request.CurrentStage != SCIHStage.Analysis || request.Status != SCIHStatus.AnalysisRework)
        {
            throw new BusinessRuleException(
                "Analysis rework can only be run right after a Director rejection. Use generate-analysis/analysis for the initial analysis.");
        }

        var latest = await _analysisRepository.GetByRequestIdAsync(requestId, ct)
            ?? throw new BusinessRuleException("No analysis has been completed for this request yet.");

        var previous = latest.PreviousAnalysisId.HasValue
            ? await _analysisRepository.GetByIdAsync(latest.PreviousAnalysisId.Value, ct)
            : null;

        var approval = await _approvalRepository.GetLatestByRequestIdAsync(requestId, ct)
            ?? throw new BusinessRuleException("This request has no approval round to rework from.");

        var solutionDesign = await _solutionDesignRepository.GetByRequestIdAsync(requestId, ct);

        var similarRequests = await _similarRequestFinder.FindTopSimilarAsync(request, topN: 5, ct: ct);
        var previousApprovedSolutions = await _knowledgeFinder.FindTopReusableAsync(request, topN: 5, ct: ct);

        var systemPrompt = _reworkPromptBuilder.BuildSystemPrompt();
        var userPrompt = _reworkPromptBuilder.BuildUserPrompt(
            request,
            JsonHelper.DeserializeObjectOrDefault<AiAnalysisResultDto>(latest.AnalysisJson),
            latest.Version,
            previous is null ? null : JsonHelper.DeserializeObjectOrDefault<AiAnalysisResultDto>(previous.AnalysisJson),
            solutionDesign is null ? null : JsonHelper.DeserializeObjectOrDefault<SolutionDesignResultDto>(solutionDesign.SolutionJson),
            approval,
            similarRequests,
            previousApprovedSolutions);

        var reworked = await GenerateAndParseAsync<AiAnalysisReworkResultDto>(
            systemPrompt, userPrompt, "analysis rework", ct);

        var now = Clock.UtcNow;
        var newAnalysis = new SCIHAnalysis
        {
            RequestId = requestId,
            AnalysisJson = JsonHelper.Serialize(reworked.Analysis),
            ComparisonJson = JsonHelper.Serialize(reworked.Comparison),
            GeneratedByModel = _modelName,
            Status = SCIHAnalysisStatus.ReworkCompleted,
            Version = latest.Version + 1,
            ReworkCount = approval.ReworkCount,
            PreviousAnalysisId = latest.Id,
            CreatedBy = _currentUser.UserId,
            CreatedDate = now
        };

        var previousRequestStatus = request.Status;
        request.Status = SCIHStatus.AnalysisReworkCompleted;
        request.ModifiedBy = _currentUser.UserId;
        request.ModifiedDate = now;
        // CurrentStage intentionally stays at Analysis — only the finer-grained
        // Status advances here; re-editing/re-approving Solution Design and
        // resubmitting is what eventually moves CurrentStage forward again.

        var histories = new List<SCIHRequestHistory>
        {
            NewHistory(requestId, SCIHHistoryAction.AnalysisReworked,
                $"AI analysis reworked to version {newAnalysis.Version} (rework count {newAnalysis.ReworkCount}).",
                previousValue: latest.AnalysisJson, newValue: newAnalysis.AnalysisJson, now),
            NewHistory(requestId, SCIHHistoryAction.RequestStatusSynced,
                $"Request status synced from '{previousRequestStatus}' to '{request.Status}' after rework completed.",
                JsonHelper.Serialize(new { status = previousRequestStatus }),
                JsonHelper.Serialize(new { status = request.Status }),
                now)
        };

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _analysisRepository.AddAsync(newAnalysis, ct);
            _requestRepository.Update(request);
            foreach (var h in histories)
                await _historyRepository.AddAsync(h, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return ToDetailDto(newAnalysis, request, reworked.Analysis, $"Analysis reworked successfully (version {newAnalysis.Version}).");
    }

    public async Task<List<AnalysisDetailDto>> GetVersionsAsync(long requestId, CancellationToken ct = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException($"Request {requestId} was not found.");

        var versions = await _analysisRepository.GetVersionsByRequestIdAsync(requestId, ct);
        return versions
            .Select(a => ToDetailDto(a, request, JsonHelper.DeserializeObjectOrDefault<AiAnalysisResultDto>(a.AnalysisJson), "Analysis version retrieved successfully."))
            .ToList();
    }

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
                    before/after the object. Keep descriptions concise so the closing brace is not truncated.
                    """;

            var rawResponse = await _claudeClient.GenerateJsonAsync(systemPrompt, attemptPrompt, ct);

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

    private static AnalysisDetailDto ToDetailDto(SCIHAnalysis analysis, SCIHRequest request, AiAnalysisResultDto result, string message) => new()
    {
        Success = true,
        Message = message,
        Id = analysis.Id,
        RequestId = analysis.RequestId,
        Analysis = result,
        Version = analysis.Version,
        ReworkCount = analysis.ReworkCount,
        PreviousAnalysisId = analysis.PreviousAnalysisId,
        AnalysisStatus = analysis.Status,
        Comparison = string.IsNullOrWhiteSpace(analysis.ComparisonJson)
            ? null
            : JsonHelper.DeserializeObjectOrDefault<AiReworkComparisonDto>(analysis.ComparisonJson),
        CurrentStage = request.CurrentStage,
        CurrentStageName = SCIHStage.NameOf(request.CurrentStage),
        Status = request.Status,
        CreatedDate = analysis.CreatedDate,
        ModifiedDate = analysis.ModifiedDate
    };

    private SCIHRequestHistory NewHistory(long requestId, string action, string? description,
        string? previousValue, string? newValue, DateTime actionDate) => new()
    {
        RequestId = requestId,
        Stage = SCIHStage.Analysis,
        StageName = SCIHStage.NameOf(SCIHStage.Analysis),
        Action = action,
        Description = description,
        PreviousValue = previousValue,
        NewValue = newValue,
        ActionBy = _currentUser.UserId,
        ActionDate = actionDate,
        IPAddress = _currentUser.IPAddress,
        DeviceInfo = _currentUser.DeviceInfo
    };
}
