using AutoMapper;
using Jarvis5.Common;
using Jarvis5.Dtos;
using Jarvis5.Dtos.Analysis;
using Jarvis5.Dtos.Approval;
using Jarvis5.Dtos.DevelopmentPlan;
using Jarvis5.Dtos.SolutionDesign;
using Jarvis5.Entities;
using Jarvis5.Repositories;
using System.Globalization;

namespace Jarvis5.Services;

public class DevelopmentPlanService : IDevelopmentPlanService
{
    private readonly IRequestRepository _requestRepository;
    private readonly IAnalysisRepository _analysisRepository;
    private readonly ISolutionDesignRepository _solutionDesignRepository;
    private readonly IApprovalRepository _approvalRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ITaskHistoryRepository _taskHistoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;

    public DevelopmentPlanService(
        IRequestRepository requestRepository,
        IAnalysisRepository analysisRepository,
        ISolutionDesignRepository solutionDesignRepository,
        IApprovalRepository approvalRepository,
        ITaskRepository taskRepository,
        ITaskHistoryRepository taskHistoryRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IMapper mapper)
    {
        _requestRepository = requestRepository;
        _analysisRepository = analysisRepository;
        _solutionDesignRepository = solutionDesignRepository;
        _approvalRepository = approvalRepository;
        _taskRepository = taskRepository;
        _taskHistoryRepository = taskHistoryRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<List<TaskModuleDetailDto>> CreateAsync(long requestId, CreateDevelopmentPlanDto dto, CancellationToken ct = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException($"Request {requestId} was not found.");

        if (request.CurrentStage < SCIHStage.Development)
            throw new BusinessRuleException("Development Planning can only start once the Director has approved the Solution Design.");

        if (dto.Modules.Count == 0)
            throw new BusinessRuleException("At least one module is required.");

        // ApprovedId/SolutionId are snapshots of the request's approval/solution-design
        // records, sourced the same way GetByRequestIdAsync below already surfaces them
        // as ApprovalDetails/ApprovedSolutionDesign.
        var latestApproval = await _approvalRepository.GetLatestByRequestIdAsync(requestId, ct);
        var solutionDesign = await _solutionDesignRepository.GetByRequestIdAsync(requestId, ct);
        var approvedId = latestApproval?.Id.ToString() ?? string.Empty;
        var solutionId = solutionDesign?.Id.ToString() ?? string.Empty;

        var now = Clock.UtcNowTz;
        var tasks = new List<SCIHTask>();

        foreach (var moduleDto in dto.Modules)
        {
            tasks.Add(new SCIHTask
            {
                RequestId = requestId.ToString(),
                ApprovedId = approvedId,
                SolutionId = solutionId,
                Module = moduleDto.Module,
                CurrentStage = SCIHStage.NameOf(request.CurrentStage),
                CurrentStatus = SCIHTaskStatus.Pending,
                DoerLead = moduleDto.DoerLead,
                OverallStartDate = ToIso(moduleDto.OverallStartDate),
                OverallEndDate = ToIso(moduleDto.OverallEndDate),
                Priority = moduleDto.Priority.Trim().ToUpperInvariant(),
                StageDetails = JsonHelper.Serialize(moduleDto.StageDetails),
                CreatedBy = _currentUser.UserId.ToString(),
                CreationDate = ToIso(now),
                IsDelete = "false"
            });
        }

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            foreach (var task in tasks)
                await _taskRepository.AddAsync(task, ct);

            // Flush first so each task gets its identity Id before history rows reference it.
            await _unitOfWork.SaveChangesAsync(ct);

            foreach (var task in tasks)
            {
                var history = NewHistory(task.Id, requestId, null, null, SCIHHistoryAction.TaskModuleCreated,
                    $"Module '{task.Module}' created.", previousValue: null, newValue: task.StageDetails, now);
                await _taskHistoryRepository.AddAsync(history, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return tasks.Select(ToTaskModuleDetailDto).ToList();
    }

    public async Task<DevelopmentPlanDto> GetByRequestIdAsync(long requestId, CancellationToken ct = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException($"Request {requestId} was not found.");

        var analysis = await _analysisRepository.GetByRequestIdAsync(requestId, ct);
        var solutionDesign = await _solutionDesignRepository.GetByRequestIdAsync(requestId, ct);
        var approval = await _approvalRepository.GetLatestByRequestIdAsync(requestId, ct);
        var tasks = await _taskRepository.GetByRequestIdAsync(requestId, ct);

        return new DevelopmentPlanDto
        {
            Request = _mapper.Map<RequestDetailDto>(request),
            LatestAnalysis = analysis is null ? null : ToAnalysisDetailDto(analysis, request),
            ApprovedSolutionDesign = solutionDesign is null ? null : ToSolutionDesignDetailDto(solutionDesign, request),
            ApprovalDetails = approval is null ? null : ToApprovalRoundDto(approval),
            Modules = tasks.Select(ToTaskModuleDetailDto).ToList()
        };
    }

    public async Task<TaskModuleDetailDto> GetModuleByIdAsync(long taskId, CancellationToken ct = default)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, ct)
            ?? throw new NotFoundException($"Task module {taskId} was not found.");

        return ToTaskModuleDetailDto(task);
    }

    public async Task<List<TaskHistoryDto>> GetHistoryByRequestIdAsync(long requestId, CancellationToken ct = default)
    {
        _ = await _requestRepository.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException($"Request {requestId} was not found.");

        var history = await _taskHistoryRepository.GetByRequestIdAsync(requestId, ct);
        return history.Select(ToTaskHistoryDto).ToList();
    }

    public async Task<TaskModuleDetailDto> UpdateModuleAsync(long taskId, UpdateTaskModuleDto dto, CancellationToken ct = default)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, ct)
            ?? throw new NotFoundException($"Task module {taskId} was not found.");

        var effectiveStart = dto.OverallStartDate ?? FromIso(task.OverallStartDate);
        var effectiveEnd = dto.OverallEndDate ?? FromIso(task.OverallEndDate);
        if (effectiveEnd < effectiveStart)
            throw new BusinessRuleException("Overall end date must not be before the overall start date.");

        var now = Clock.UtcNowTz;
        var requestId = long.Parse(task.RequestId);
        var histories = new List<SCIHTaskHistory>();

        var previousSnapshot = JsonHelper.Serialize(new { task.Module, task.OverallStartDate, task.OverallEndDate, task.Priority });

        if (dto.Module is not null)
            task.Module = dto.Module;

        if (dto.DoerLead is not null)
            task.DoerLead = dto.DoerLead;

        if (dto.OverallStartDate.HasValue)
            task.OverallStartDate = ToIso(dto.OverallStartDate.Value);

        if (dto.OverallEndDate.HasValue)
            task.OverallEndDate = ToIso(dto.OverallEndDate.Value);

        if (dto.Priority is not null)
            task.Priority = dto.Priority.Trim().ToUpperInvariant();

        if (dto.StageDetails is not null)
            task.StageDetails = JsonHelper.Serialize(dto.StageDetails);

        task.UpdatedBy = _currentUser.UserId.ToString();
        task.UpdationDate = ToIso(now);

        var newSnapshot = JsonHelper.Serialize(new { task.Module, task.OverallStartDate, task.OverallEndDate, task.Priority });
        histories.Add(NewHistory(task.Id, requestId, null, null, SCIHHistoryAction.TaskModuleUpdated,
            $"Module '{task.Module}' updated.", previousSnapshot, newSnapshot, now));

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            _taskRepository.Update(task);
            foreach (var h in histories)
                await _taskHistoryRepository.AddAsync(h, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return ToTaskModuleDetailDto(task);
    }

    public async Task<TaskModuleDetailDto> UpdateStageAsync(long taskId, long stageId, UpdateStageDto dto, CancellationToken ct = default)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, ct)
            ?? throw new NotFoundException($"Task module {taskId} was not found.");

        var stages = JsonHelper.DeserializeList<StageDetailDto>(task.StageDetails);
        var stage = stages.FirstOrDefault(s => s.StageId == stageId)
            ?? throw new NotFoundException($"Stage {stageId} was not found on module {taskId}.");

        var now = Clock.UtcNowTz;
        var previousJson = JsonHelper.Serialize(stage);

        stage.DoerId = dto.DoerId;
        stage.Description = dto.Description;
        stage.PlannedStart = dto.PlannedStart;
        stage.PlannedEnd = dto.PlannedEnd;
        stage.ActualStart = dto.ActualStart;
        stage.ActualEnd = dto.ActualEnd;
        stage.Status = dto.Status;
        stage.RrrCount = dto.RrrCount;
        stage.Remarks = dto.Remarks;
        if (dto.Checklist.Count > 0)
            stage.Checklist = dto.Checklist;

        var newJson = JsonHelper.Serialize(stage);

        task.StageDetails = JsonHelper.Serialize(stages);
        task.UpdatedBy = _currentUser.UserId.ToString();
        task.UpdationDate = ToIso(now);

        var history = NewHistory(task.Id, long.Parse(task.RequestId), stage.StageId, stage.StageName,
            SCIHHistoryAction.TaskStageUpdated, $"Stage '{stage.StageName}' updated on module '{task.Module}'.",
            previousJson, newJson, now);
        if (!string.IsNullOrWhiteSpace(dto.Remarks))
            history.Remarks = dto.Remarks;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            _taskRepository.Update(task);
            await _taskHistoryRepository.AddAsync(history, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return ToTaskModuleDetailDto(task);
    }

    public async Task DeleteModuleAsync(long taskId, CancellationToken ct = default)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, ct)
            ?? throw new NotFoundException($"Task module {taskId} was not found.");

        if (task.IsDelete == "true")
            throw new BusinessRuleException("This module has already been deleted.");

        var now = Clock.UtcNowTz;
        var userId = _currentUser.UserId.ToString();
        task.IsDelete = "true";
        task.IsDeletedBy = userId;
        task.UpdatedBy = userId;
        task.UpdationDate = ToIso(now);

        var history = NewHistory(task.Id, long.Parse(task.RequestId), null, null, SCIHHistoryAction.TaskModuleDeleted,
            $"Module '{task.Module}' deleted.", previousValue: null, newValue: null, now);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            _taskRepository.Update(task);
            await _taskHistoryRepository.AddAsync(history, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);
    }

    /// <summary>Incoming JSON without a "Z"/offset deserializes as Kind=Unspecified —
    /// relabel it (this app treats every DateTime as UTC wall-clock; see Clock.cs)
    /// before formatting it for the SCIH_Task date columns, which are plain ISO-8601
    /// strings rather than real timestamp columns.</summary>
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string ToIso(DateTime value) => AsUtc(value).ToString("O", CultureInfo.InvariantCulture);

    private static DateTime FromIso(string value) => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static DateTime? FromIsoOrNull(string? value) => string.IsNullOrEmpty(value) ? null : FromIso(value);

    private SCIHTaskHistory NewHistory(long taskId, long requestId, long? stageId, string? stageName,
        string action, string? description, string? previousValue, string? newValue, DateTime actionDate) => new()
    {
        TaskId = taskId,
        RequestId = requestId,
        StageId = stageId,
        StageName = stageName,
        Action = action,
        Description = description,
        PreviousValue = previousValue,
        NewValue = newValue,
        ActionBy = _currentUser.UserId,
        ActionDate = actionDate,
        IPAddress = _currentUser.IPAddress,
        DeviceInfo = _currentUser.DeviceInfo
    };

    private static TaskModuleDetailDto ToTaskModuleDetailDto(SCIHTask task) => new()
    {
        Id = task.Id,
        RequestId = long.Parse(task.RequestId),
        ApprovedId = task.ApprovedId,
        SolutionId = task.SolutionId,
        Module = task.Module,
        CurrentStage = task.CurrentStage,
        CurrentStatus = task.CurrentStatus,
        StageDetails = JsonHelper.ParseOrNull(task.StageDetails) ?? new List<object>(),
        DoerLead = task.DoerLead,
        OverallStartDate = FromIso(task.OverallStartDate),
        OverallEndDate = FromIso(task.OverallEndDate),
        Priority = task.Priority,
        CreationDate = FromIso(task.CreationDate),
        CreatedBy = task.CreatedBy,
        UpdationDate = FromIsoOrNull(task.UpdationDate),
        UpdatedBy = task.UpdatedBy,
        IsDelete = task.IsDelete,
        IsDeletedBy = task.IsDeletedBy
    };

    private static TaskHistoryDto ToTaskHistoryDto(SCIHTaskHistory h) => new()
    {
        Id = h.Id,
        TaskId = h.TaskId,
        RequestId = h.RequestId ?? 0,
        StageId = h.StageId,
        StageName = h.StageName,
        Action = h.Action,
        Description = h.Description,
        PreviousValue = JsonHelper.ParseOrNull(h.PreviousValue),
        NewValue = JsonHelper.ParseOrNull(h.NewValue),
        Remarks = h.Remarks,
        ActionBy = h.ActionBy,
        ActionDate = h.ActionDate,
        IPAddress = h.IPAddress,
        DeviceInfo = h.DeviceInfo
    };

    private static AnalysisDetailDto ToAnalysisDetailDto(SCIHAnalysis analysis, SCIHRequest request) => new()
    {
        Id = analysis.Id,
        RequestId = analysis.RequestId,
        Analysis = JsonHelper.DeserializeObjectOrDefault<AiAnalysisResultDto>(analysis.AnalysisJson),
        Version = analysis.Version,
        ReworkCount = analysis.ReworkCount,
        PreviousAnalysisId = analysis.PreviousAnalysisId,
        AnalysisStatus = analysis.Status,
        CurrentStage = request.CurrentStage,
        CurrentStageName = SCIHStage.NameOf(request.CurrentStage),
        Status = request.Status,
        CreatedDate = analysis.CreatedDate,
        ModifiedDate = analysis.ModifiedDate
    };

    private static SolutionDesignDetailDto ToSolutionDesignDetailDto(SCIHSolutionDesign design, SCIHRequest request) => new()
    {
        Id = design.Id,
        RequestId = design.RequestId,
        Solution = JsonHelper.DeserializeObjectOrDefault<SolutionDesignResultDto>(design.SolutionJson),
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

    private static ApprovalRoundDto ToApprovalRoundDto(SCIHApproval approval) => new()
    {
        Id = approval.Id,
        ApprovalRound = approval.ApprovalRound,
        ReworkCount = approval.ReworkCount,
        Decision = approval.Decision,
        Comments = approval.Comments,
        RejectionReason = approval.RejectionReason,
        ImprovementAreas = JsonHelper.DeserializeList<string>(approval.ImprovementAreasJson),
        ApprovedBy = approval.ApprovedBy,
        ApprovedDate = approval.ApprovedDate,
        CreatedDate = approval.CreatedDate
    };
}
