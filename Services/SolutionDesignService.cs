using Jarvis5.Common;
using Jarvis5.Dtos;
using Jarvis5.Dtos.Analysis;
using Jarvis5.Dtos.SolutionDesign;
using Jarvis5.Entities;
using Jarvis5.Repositories;
using Jarvis5.Services.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jarvis5.Services;

public class SolutionDesignService : ISolutionDesignService
{
    private readonly IRequestRepository _requestRepository;
    private readonly IAnalysisRepository _analysisRepository;
    private readonly ISolutionDesignRepository _solutionDesignRepository;
    private readonly IRequestHistoryRepository _historyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISolutionKnowledgeFinder _knowledgeFinder;
    private readonly ISolutionDesignPromptBuilder _promptBuilder;
    private readonly IClaudeClient _claudeClient;
    private readonly ICurrentUserService _currentUser;
    private readonly IAttachmentFileService _attachmentFileService;
    private readonly ILogger<SolutionDesignService> _logger;
    private readonly string _modelName;

    public SolutionDesignService(
        IRequestRepository requestRepository,
        IAnalysisRepository analysisRepository,
        ISolutionDesignRepository solutionDesignRepository,
        IRequestHistoryRepository historyRepository,
        IUnitOfWork unitOfWork,
        ISolutionKnowledgeFinder knowledgeFinder,
        ISolutionDesignPromptBuilder promptBuilder,
        IClaudeClient claudeClient,
        ICurrentUserService currentUser,
        IAttachmentFileService attachmentFileService,
        ILogger<SolutionDesignService> logger,
        IOptions<ClaudeOptions> claudeOptions)
    {
        _requestRepository = requestRepository;
        _analysisRepository = analysisRepository;
        _solutionDesignRepository = solutionDesignRepository;
        _historyRepository = historyRepository;
        _unitOfWork = unitOfWork;
        _knowledgeFinder = knowledgeFinder;
        _promptBuilder = promptBuilder;
        _claudeClient = claudeClient;
        _currentUser = currentUser;
        _attachmentFileService = attachmentFileService;
        _logger = logger;
        _modelName = claudeOptions.Value.Model;
    }

    public async Task<SolutionDesignDetailDto> GenerateAsync(long requestId, CancellationToken ct = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException($"Request {requestId} was not found.");

        EnsureEditableStage(request, "generated");

        var analysisEntity = await _analysisRepository.GetByRequestIdAsync(requestId, ct)
            ?? throw new BusinessRuleException("No analysis has been completed for this request yet.");
        var analysis = JsonHelper.DeserializeObjectOrDefault<AiAnalysisResultDto>(analysisEntity.AnalysisJson);

        var knowledgeBase = await _knowledgeFinder.FindTopReusableAsync(request, topN: 5, ct: ct);

        var systemPrompt = _promptBuilder.BuildSystemPrompt();
        var userPrompt = _promptBuilder.BuildUserPrompt(request, analysis, knowledgeBase);

        var rawResponse = await _claudeClient.GenerateJsonAsync(systemPrompt, userPrompt, ct);
        var result = AiJsonResponseParser.Parse<SolutionDesignResultDto>(rawResponse, _logger, "solution design");

        var now = Clock.UtcNow;
        var newJson = JsonHelper.Serialize(result);

        var existing = await _solutionDesignRepository.GetByRequestIdAsync(requestId, ct);
        SCIHSolutionDesign design;
        SCIHRequestHistory history;

        if (existing is null)
        {
            design = new SCIHSolutionDesign
            {
                RequestId = requestId,
                SolutionJson = newJson,
                AIModel = _modelName,
                PromptVersion = _promptBuilder.PromptVersion,
                GeneratedAt = now,
                GeneratedBy = _currentUser.UserId,
                IsEdited = false,
                Status = SCIHSolutionDesignStatus.Draft,
                Version = 1,
                CreatedDate = now
            };

            history = NewHistory(requestId, SCIHHistoryAction.SolutionDesignGenerated,
                "AI solution design generated.", previousValue: null, newValue: newJson, now);
        }
        else
        {
            if (existing.Status == SCIHSolutionDesignStatus.Approved)
                throw new BusinessRuleException("This solution design has already been approved and cannot be regenerated.");

            design = existing;
            history = NewHistory(requestId, SCIHHistoryAction.SolutionDesignGenerated,
                "AI solution design regenerated.", previousValue: existing.SolutionJson, newValue: newJson, now);

            design.SolutionJson = newJson;
            design.AIModel = _modelName;
            design.PromptVersion = _promptBuilder.PromptVersion;
            design.GeneratedAt = now;
            design.GeneratedBy = _currentUser.UserId;
            design.IsEdited = false;
            design.Status = SCIHSolutionDesignStatus.Draft;
            design.Version += 1;
            design.ModifiedDate = now;
        }

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            if (existing is null)
                await _solutionDesignRepository.AddAsync(design, ct);
            else
                _solutionDesignRepository.Update(design);

            await _historyRepository.AddAsync(history, ct);

            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return ToDetailDto(design, request, result);
    }

    public async Task<SolutionDesignDetailDto> GetByRequestIdAsync(long requestId, CancellationToken ct = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException($"Request {requestId} was not found.");

        var design = await _solutionDesignRepository.GetByRequestIdAsync(requestId, ct)
            ?? throw new NotFoundException($"No solution design has been generated yet for request {requestId}.");

        return ToDetailDto(design, request, JsonHelper.DeserializeObjectOrDefault<SolutionDesignResultDto>(design.SolutionJson));
    }

    public async Task<SolutionDesignDetailDto> UpdateAsync(long requestId, UpdateSolutionDesignDto dto, CancellationToken ct = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException($"Request {requestId} was not found.");

        EnsureEditableStage(request, "edited");

        var design = await _solutionDesignRepository.GetByRequestIdAsync(requestId, ct)
            ?? throw new NotFoundException($"No solution design has been generated yet for request {requestId}.");

        if (design.Status == SCIHSolutionDesignStatus.Approved)
            throw new BusinessRuleException("This solution design has already been approved and cannot be edited.");

        var now = Clock.UtcNow;
        var newJson = JsonHelper.Serialize(dto.Solution);

        var histories = new List<SCIHRequestHistory>();

        var history = NewHistory(requestId, SCIHHistoryAction.SolutionDesignUpdated,
            "Solution design updated.", previousValue: design.SolutionJson, newValue: newJson, now);
        if (!string.IsNullOrWhiteSpace(dto.Remarks))
            history.Remarks = dto.Remarks;
        histories.Add(history);

        var oldAttachments = JsonHelper.DeserializeList<AttachmentDto>(design.AttachmentsJson);
        var (mergedAttachments, attachmentHistories) = DiffAttachments(requestId, oldAttachments, dto.Attachments, now);
        histories.AddRange(attachmentHistories);

        design.SolutionJson = newJson;
        design.AttachmentsJson = JsonHelper.Serialize(mergedAttachments);
        design.IsEdited = true;
        design.Status = SCIHSolutionDesignStatus.Reviewed;
        design.Version += 1;
        design.ModifiedDate = now;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            _solutionDesignRepository.Update(design);
            foreach (var h in histories)
                await _historyRepository.AddAsync(h, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return ToDetailDto(design, request, dto.Solution);
    }

    private (List<AttachmentDto> Merged, List<SCIHRequestHistory> Histories) DiffAttachments(
        long requestId, List<AttachmentDto> oldItems, List<AttachmentDto> newItems, DateTime now)
    {
        var histories = new List<SCIHRequestHistory>();
        var nextId = oldItems.Count == 0 ? 1 : oldItems.Max(a => a.Id) + 1;
        var merged = new List<AttachmentDto>();
        var seenOldIds = new HashSet<long>();

        foreach (var item in newItems)
        {
            var existing = item.Id > 0 ? oldItems.FirstOrDefault(o => o.Id == item.Id) : null;
            if (existing is not null)
            {
                seenOldIds.Add(existing.Id);
                merged.Add(item);
            }
            else
            {
                var saved = _attachmentFileService.Persist(item);
                var created = new AttachmentDto
                {
                    Id = nextId++,
                    FileName = saved.FileName,
                    OriginalName = saved.OriginalName,
                    FileUrl = saved.FileUrl,
                    ContentType = saved.ContentType,
                    FileSize = saved.FileSize,
                    UploadedBy = saved.UploadedBy > 0 ? saved.UploadedBy : _currentUser.UserId,
                    UploadedAt = saved.UploadedAt == default ? now : saved.UploadedAt
                };
                merged.Add(created);
                histories.Add(NewHistory(requestId, SCIHHistoryAction.AttachmentAdded,
                    $"Attachment '{created.FileName}' added.", null,
                    JsonHelper.Serialize(new { fileName = created.FileName, uploadedBy = created.UploadedBy, uploadedAt = created.UploadedAt }),
                    now));
            }
        }

        foreach (var removed in oldItems.Where(o => !seenOldIds.Contains(o.Id)))
        {
            histories.Add(NewHistory(requestId, SCIHHistoryAction.AttachmentRemoved,
                $"Attachment '{removed.FileName}' removed.",
                JsonHelper.Serialize(new { fileName = removed.FileName, uploadedBy = removed.UploadedBy, uploadedAt = removed.UploadedAt }),
                null, now));
        }

        return (merged, histories);
    }

    public async Task<SolutionDesignDetailDto> ApproveAsync(long requestId, ApproveSolutionDesignDto? dto, CancellationToken ct = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException($"Request {requestId} was not found.");

        EnsureEditableStage(request, "approved");

        var design = await _solutionDesignRepository.GetByRequestIdAsync(requestId, ct)
            ?? throw new NotFoundException($"No solution design has been generated yet for request {requestId}.");

        if (design.Status == SCIHSolutionDesignStatus.Approved)
            throw new BusinessRuleException("This solution design has already been approved.");

        var now = Clock.UtcNow;
        var previousStage = request.CurrentStage;

        design.Status = SCIHSolutionDesignStatus.Approved;
        design.ModifiedDate = now;

        request.CurrentStage = SCIHStage.SolutionDesign;
        request.Status = SCIHStatus.SolutionDesign;
        request.ModifiedBy = _currentUser.UserId;
        request.ModifiedDate = now;

        var approvalHistory = NewHistory(requestId, SCIHHistoryAction.SolutionDesignApproved,
            "Solution design approved.", previousValue: null, newValue: design.SolutionJson, now);
        if (!string.IsNullOrWhiteSpace(dto?.Remarks))
            approvalHistory.Remarks = dto.Remarks;

        var stageHistory = NewHistory(requestId, SCIHHistoryAction.StageChanged,
            $"Stage changed from '{SCIHStage.NameOf(previousStage)}' to '{SCIHStage.NameOf(SCIHStage.SolutionDesign)}'.",
            JsonHelper.Serialize(new { stage = previousStage, stageName = SCIHStage.NameOf(previousStage) }),
            JsonHelper.Serialize(new { stage = SCIHStage.SolutionDesign, stageName = SCIHStage.NameOf(SCIHStage.SolutionDesign) }),
            now);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            _solutionDesignRepository.Update(design);
            _requestRepository.Update(request);
            await _historyRepository.AddAsync(approvalHistory, ct);
            await _historyRepository.AddAsync(stageHistory, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return ToDetailDto(design, request, JsonHelper.DeserializeObjectOrDefault<SolutionDesignResultDto>(design.SolutionJson));
    }

    /// <summary>Solution Design can only be generated/edited/approved while the request
    /// sits at Stage 2 (Analysis) — i.e. analysis is complete but the design isn't
    /// approved yet. Approval is what advances the request to Stage 3.</summary>
    private static void EnsureEditableStage(SCIHRequest request, string attemptedAction)
    {
        if (request.CurrentStage < SCIHStage.Analysis)
            throw new BusinessRuleException("The Analysis stage must be completed before a Solution Design can be created.");

        if (request.CurrentStage > SCIHStage.Analysis)
            throw new BusinessRuleException(
                $"This request is already at stage '{SCIHStage.NameOf(request.CurrentStage)}'; the solution design can no longer be {attemptedAction}.");
    }

    private SCIHRequestHistory NewHistory(long requestId, string action, string? description,
        string? previousValue, string? newValue, DateTime actionDate) => new()
    {
        RequestId = requestId,
        Stage = SCIHStage.SolutionDesign,
        StageName = SCIHStage.NameOf(SCIHStage.SolutionDesign),
        Action = action,
        Description = description,
        PreviousValue = previousValue,
        NewValue = newValue,
        ActionBy = _currentUser.UserId,
        ActionDate = actionDate,
        IPAddress = _currentUser.IPAddress,
        DeviceInfo = _currentUser.DeviceInfo
    };

    private static SolutionDesignDetailDto ToDetailDto(SCIHSolutionDesign design, SCIHRequest request, SolutionDesignResultDto solution) => new()
    {
        Id = design.Id,
        RequestId = design.RequestId,
        Solution = solution,
        Attachments = JsonHelper.DeserializeList<AttachmentDto>(design.AttachmentsJson),
        AIModel = design.AIModel,
        PromptVersion = design.PromptVersion,
        GeneratedAt = design.GeneratedAt,
        GeneratedBy = design.GeneratedBy,
        IsEdited = design.IsEdited,
        Status = design.Status,
        Version = design.Version,
        CurrentStage = request.CurrentStage,
        CurrentStageName = SCIHStage.NameOf(request.CurrentStage),
        RequestStatus = request.Status,
        CreatedDate = design.CreatedDate,
        ModifiedDate = design.ModifiedDate
    };
}
