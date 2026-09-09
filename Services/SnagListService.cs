using Jarvis5.Common;
using Jarvis5.Dtos.SnagList;
using Jarvis5.Entities;
using Jarvis5.Repositories;
using System.Globalization;

namespace Jarvis5.Services;

public class SnagListService : ISnagListService
{
    /// <summary>Stages that must be present and Completed before a snag list can be
    /// closed, in addition to the general "every selected stage is Completed" rule —
    /// see CloseAsync.</summary>
    private static readonly string[] MandatoryStageNames =
    {
        "Final Feedback",
        "Technical Documentation",
        "AI Documentation Review",
        "User Training Documentation"
    };

    private readonly ISnagListRepository _snagListRepository;
    private readonly ITaskHistoryRepository _taskHistoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public SnagListService(
        ISnagListRepository snagListRepository,
        ITaskHistoryRepository taskHistoryRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _snagListRepository = snagListRepository;
        _taskHistoryRepository = taskHistoryRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<SnagListDetailDto> CreateAsync(CreateSnagListDto dto, CancellationToken ct = default)
    {
        // RequestId/TaskId are optional and, when given, are stored as free-form
        // references — not validated against SCIH_Request/SCIH_Task — so a Snag
        // List can be raised standalone with no existing project/request behind it.
        if (dto.StageDetails.Count == 0)
            throw new BusinessRuleException("At least one stage is required.");

        var now = Clock.UtcNowTz;
        var snag = new SCIHSnagList
        {
            RequestId = dto.RequestId?.ToString(),
            TaskId = dto.TaskId?.ToString(),
            Module = dto.Module,
            SnagDescription = dto.SnagDescription,
            Priority = dto.Priority.Trim().ToUpperInvariant(),
            CurrentStatus = SCIHSnagStatus.Open,
            StageDetails = JsonHelper.Serialize(dto.StageDetails),
            CreatedBy = _currentUser.UserId.ToString(),
            CreationDate = ToIso(now),
            IsDelete = "false"
        };

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _snagListRepository.AddAsync(snag, ct);

            // Flush first so the snag gets its identity Id before the history row references it.
            await _unitOfWork.SaveChangesAsync(ct);

            var history = NewHistory(snag.Id, dto.RequestId, null, null, SCIHHistoryAction.SnagListCreated,
                $"Snag List '{snag.Module}' created.", previousValue: null, newValue: snag.StageDetails, now);
            await _taskHistoryRepository.AddAsync(history, ct);

            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return ToSnagListDetailDto(snag);
    }

    public async Task<List<SnagListDetailDto>> GetByDoerIdAsync(string employeeId, CancellationToken ct = default)
    {
        var snags = await _snagListRepository.GetAllAsync(ct);

        return snags
            .Where(s => JsonHelper.DeserializeList<SnagStageDetailDto>(s.StageDetails)
                .Any(stage => stage.Selected && string.Equals(stage.DoerId, employeeId, StringComparison.OrdinalIgnoreCase)))
            .Select(ToSnagListDetailDto)
            .ToList();
    }

    public async Task<SnagListDto> GetByIdAsync(long snagId, CancellationToken ct = default)
    {
        var snag = await _snagListRepository.GetByIdAsync(snagId, ct)
            ?? throw new NotFoundException($"Snag List {snagId} was not found.");

        var history = await _taskHistoryRepository.GetByTaskIdAsync(snag.Id, ct);

        return new SnagListDto
        {
            SnagDetails = ToSnagListDetailDto(snag),
            History = history.Select(ToSnagHistoryDto).ToList()
        };
    }

    public async Task<SnagListDetailDto> UpdateAsync(long snagId, UpdateSnagListDto dto, CancellationToken ct = default)
    {
        var snag = await _snagListRepository.GetByIdAsync(snagId, ct)
            ?? throw new NotFoundException($"Snag List {snagId} was not found.");

        if (string.Equals(dto.CurrentStatus, SCIHSnagStatus.Closed, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Use POST /api/snaglist/{id}/close to close a Snag List.");

        var now = Clock.UtcNowTz;
        var previousSnapshot = JsonHelper.Serialize(new
        {
            snag.RequestId,
            snag.TaskId,
            snag.Module,
            snag.SnagDescription,
            snag.Priority,
            snag.CurrentStatus,
            snag.StageDetails
        });

        if (dto.RequestId.HasValue)
            snag.RequestId = dto.RequestId.Value.ToString();

        if (dto.TaskId.HasValue)
            snag.TaskId = dto.TaskId.Value.ToString();

        if (dto.Module is not null)
            snag.Module = dto.Module;

        if (dto.SnagDescription is not null)
            snag.SnagDescription = dto.SnagDescription;

        if (dto.Priority is not null)
            snag.Priority = dto.Priority.Trim().ToUpperInvariant();

        if (dto.CurrentStatus is not null)
            snag.CurrentStatus = dto.CurrentStatus;

        if (dto.StageDetails is not null)
            snag.StageDetails = JsonHelper.Serialize(dto.StageDetails);

        snag.UpdatedBy = _currentUser.UserId.ToString();
        snag.UpdationDate = ToIso(now);

        var newSnapshot = JsonHelper.Serialize(new
        {
            snag.RequestId,
            snag.TaskId,
            snag.Module,
            snag.SnagDescription,
            snag.Priority,
            snag.CurrentStatus,
            snag.StageDetails
        });
        var history = NewHistory(snag.Id, ParseNullable(snag.RequestId), null, null, SCIHHistoryAction.SnagListUpdated,
            $"Snag List '{snag.Module}' updated.", previousSnapshot, newSnapshot, now);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            _snagListRepository.Update(snag);
            await _taskHistoryRepository.AddAsync(history, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return ToSnagListDetailDto(snag);
    }

    public async Task<SnagListDetailDto> UpdateStageAsync(long snagId, long stageId, UpdateSnagStageDto dto, CancellationToken ct = default)
    {
        var snag = await _snagListRepository.GetByIdAsync(snagId, ct)
            ?? throw new NotFoundException($"Snag List {snagId} was not found.");

        var stages = JsonHelper.DeserializeList<SnagStageDetailDto>(snag.StageDetails);
        var stage = stages.FirstOrDefault(s => s.StageId == stageId)
            ?? throw new NotFoundException($"Stage {stageId} was not found on Snag List {snagId}.");

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

        snag.StageDetails = JsonHelper.Serialize(stages);
        snag.UpdatedBy = _currentUser.UserId.ToString();
        snag.UpdationDate = ToIso(now);

        var history = NewHistory(snag.Id, ParseNullable(snag.RequestId), stage.StageId, stage.StageName,
            SCIHHistoryAction.SnagStageUpdated, $"Stage '{stage.StageName}' updated on Snag List '{snag.Module}'.",
            previousJson, newJson, now);
        if (!string.IsNullOrWhiteSpace(dto.Remarks))
            history.Remarks = dto.Remarks;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            _snagListRepository.Update(snag);
            await _taskHistoryRepository.AddAsync(history, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return ToSnagListDetailDto(snag);
    }

    public async Task<SnagListDetailDto> CloseAsync(long snagId, CancellationToken ct = default)
    {
        var snag = await _snagListRepository.GetByIdAsync(snagId, ct)
            ?? throw new NotFoundException($"Snag List {snagId} was not found.");

        if (snag.CurrentStatus == SCIHSnagStatus.Closed)
            throw new BusinessRuleException("This Snag List has already been closed.");

        var stages = JsonHelper.DeserializeList<SnagStageDetailDto>(snag.StageDetails)
            .Where(s => s.Selected)
            .ToList();

        if (stages.Count == 0)
            throw new BusinessRuleException("At least one stage must be selected before closing.");

        foreach (var name in MandatoryStageNames)
        {
            var stage = stages.FirstOrDefault(s => string.Equals(s.StageName, name, StringComparison.OrdinalIgnoreCase));
            if (stage is null)
                throw new BusinessRuleException($"'{name}' must be selected and completed before closing this Snag List.");
            if (stage.Status != SCIHTaskStatus.Completed)
                throw new BusinessRuleException($"'{name}' must be completed before closing this Snag List.");
        }

        var incomplete = stages.Where(s => s.Status != SCIHTaskStatus.Completed).Select(s => s.StageName).ToList();
        if (incomplete.Count > 0)
            throw new BusinessRuleException($"All stages must be completed before closing: {string.Join(", ", incomplete)}.");

        var now = Clock.UtcNowTz;
        var previousStatus = snag.CurrentStatus;
        snag.CurrentStatus = SCIHSnagStatus.Closed;
        snag.UpdatedBy = _currentUser.UserId.ToString();
        snag.UpdationDate = ToIso(now);

        var history = NewHistory(snag.Id, ParseNullable(snag.RequestId), null, null, SCIHHistoryAction.SnagListClosed,
            $"Snag List '{snag.Module}' closed.",
            previousValue: JsonHelper.Serialize(new { status = previousStatus }),
            newValue: JsonHelper.Serialize(new { status = snag.CurrentStatus }), now);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            _snagListRepository.Update(snag);
            await _taskHistoryRepository.AddAsync(history, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return ToSnagListDetailDto(snag);
    }

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string ToIso(DateTime value) => AsUtc(value).ToString("O", CultureInfo.InvariantCulture);

    private static DateTime FromIso(string value) => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static DateTime? FromIsoOrNull(string? value) => string.IsNullOrEmpty(value) ? null : FromIso(value);

    private static long? ParseNullable(string? value) => string.IsNullOrEmpty(value) ? null : long.Parse(value);

    private SCIHTaskHistory NewHistory(long snagId, long? requestId, long? stageId, string? stageName,
        string action, string? description, string? previousValue, string? newValue, DateTime actionDate) => new()
    {
        TaskId = snagId,
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

    private static SnagListDetailDto ToSnagListDetailDto(SCIHSnagList snag) => new()
    {
        Id = snag.Id,
        RequestId = ParseNullable(snag.RequestId),
        TaskId = ParseNullable(snag.TaskId),
        Module = snag.Module,
        SnagDescription = snag.SnagDescription,
        Priority = snag.Priority,
        CurrentStatus = snag.CurrentStatus,
        StageDetails = JsonHelper.ParseOrNull(snag.StageDetails) ?? new List<object>(),
        CreationDate = FromIso(snag.CreationDate),
        CreatedBy = snag.CreatedBy,
        UpdationDate = FromIsoOrNull(snag.UpdationDate),
        UpdatedBy = snag.UpdatedBy,
        IsDelete = snag.IsDelete,
        IsDeletedBy = snag.IsDeletedBy
    };

    private static SnagHistoryDto ToSnagHistoryDto(SCIHTaskHistory h) => new()
    {
        Id = h.Id,
        SnagId = h.TaskId,
        RequestId = h.RequestId,
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
}
