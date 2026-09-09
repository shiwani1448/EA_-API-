using System.Text.Json.Nodes;
using AutoMapper;
using Jarvis5.Common;
using Jarvis5.Dtos;
using Jarvis5.Entities;
using Jarvis5.Repositories;

namespace Jarvis5.Services;

public class RequestService : IRequestService
{
    private readonly IRequestRepository _requestRepository;
    private readonly IRequestHistoryRepository _historyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;
    private readonly IAttachmentFileService _attachmentFileService;

    public RequestService(
        IRequestRepository requestRepository,
        IRequestHistoryRepository historyRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ICurrentUserService currentUser,
        IAttachmentFileService attachmentFileService)
    {
        _requestRepository = requestRepository;
        _historyRepository = historyRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _currentUser = currentUser;
        _attachmentFileService = attachmentFileService;
    }

    public async Task<RequestDetailDto> CreateAsync(CreateRequestDto dto, CancellationToken ct = default)
    {
        if (dto.ParentRequestId.HasValue &&
            !await _requestRepository.ExistsIgnoringSoftDeleteAsync(dto.ParentRequestId.Value, ct))
        {
            throw new NotFoundException($"Parent request {dto.ParentRequestId} was not found.");
        }

        var now = Clock.UtcNow;
        var painPoints = AssignIds(dto.PainPoints);
        var attachments = AssignIds(dto.Attachments, now);

        var entity = new SCIHRequest
        {
            RequestNo = await _requestRepository.GenerateNextRequestNoAsync(ct),
            Title = dto.Title.Trim(),
            DepartmentId = dto.DepartmentId.Trim(),
            RaisedBy = dto.RaisedBy.Trim(),
            RaisedAt = now,
            Status = SCIHStatus.Raised,
            CurrentStage = SCIHStage.RequestRaised,
            Priority = dto.Priority.Trim().ToUpperInvariant(),
            OverallProgress = 0m,
            ExpectedBenefit = dto.ExpectedBenefit.Trim(),
            ParentRequestId = dto.ParentRequestId,
            PainPointsJson = JsonHelper.Serialize(painPoints),
            AttachmentsJson = JsonHelper.Serialize(attachments),
            MetaJson = JsonHelper.Serialize(dto.Meta ?? new RequestMetaDto()),
            IsDeleted = false,
            CreatedBy = _currentUser.UserId,
            CreatedDate = now
        };

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _requestRepository.AddAsync(entity, ct);
            await _unitOfWork.SaveChangesAsync(ct); // populates entity.Id for the FK below

            await _historyRepository.AddAsync(NewHistory(
                entity.Id,
                SCIHHistoryAction.RequestCreated,
                "New Request Raised",
                previousValue: null,
                newValue: JsonHelper.Serialize(new { requestNo = entity.RequestNo, title = entity.Title }),
                now), ct);

            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return await GetByIdAsync(entity.Id, ct);
    }

    public async Task<RequestDetailDto> UpdateAsync(long id, UpdateRequestDto dto, CancellationToken ct = default)
    {
        var entity = await _requestRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Request {id} was not found.");

        if (entity.Status != SCIHStatus.Raised)
            throw new BusinessRuleException("This request can no longer be edited because it is not in RAISED status.");

        var now = Clock.UtcNow;
        var histories = new List<SCIHRequestHistory>();

        var newTitle = dto.Title.Trim();
        var newDepartmentId = dto.DepartmentId.Trim();
        var newRaisedBy = dto.RaisedBy.Trim();
        var newPriority = dto.Priority.Trim().ToUpperInvariant();
        var newExpectedBenefit = dto.ExpectedBenefit.Trim();

        AddFieldChangeHistory(histories, entity.Id, "title", "Title", entity.Title, newTitle, now);
        AddFieldChangeHistory(histories, entity.Id, "departmentId", "Department", entity.DepartmentId, newDepartmentId, now);
        AddFieldChangeHistory(histories, entity.Id, "raisedBy", "Raised By", entity.RaisedBy, newRaisedBy, now);
        AddFieldChangeHistory(histories, entity.Id, "priority", "Priority", entity.Priority, newPriority, now);
        AddFieldChangeHistory(histories, entity.Id, "expectedBenefit", "Expected Benefit", entity.ExpectedBenefit, newExpectedBenefit, now);

        var newMetaJson = JsonHelper.Serialize(dto.Meta ?? new RequestMetaDto());
        if (!JsonEquivalent(entity.MetaJson, newMetaJson))
        {
            histories.Add(NewHistory(entity.Id, SCIHHistoryAction.RequestUpdated,
                "Meta information updated.", entity.MetaJson, newMetaJson, now));
        }

        var oldPainPoints = JsonHelper.DeserializeList<PainPointDto>(entity.PainPointsJson);
        var (mergedPainPoints, painPointHistories) = DiffPainPoints(entity.Id, oldPainPoints, dto.PainPoints, now);
        histories.AddRange(painPointHistories);

        var oldAttachments = JsonHelper.DeserializeList<AttachmentDto>(entity.AttachmentsJson);
        var (mergedAttachments, attachmentHistories) = DiffAttachments(entity.Id, oldAttachments, dto.Attachments, now);
        histories.AddRange(attachmentHistories);

        if (histories.Count > 0 && !string.IsNullOrWhiteSpace(dto.Remarks))
            histories[^1].Remarks = dto.Remarks;

        entity.Title = newTitle;
        entity.DepartmentId = newDepartmentId;
        entity.RaisedBy = newRaisedBy;
        entity.Priority = newPriority;
        entity.ExpectedBenefit = newExpectedBenefit;
        entity.PainPointsJson = JsonHelper.Serialize(mergedPainPoints);
        entity.AttachmentsJson = JsonHelper.Serialize(mergedAttachments);
        entity.MetaJson = newMetaJson;
        entity.ModifiedBy = _currentUser.UserId;
        entity.ModifiedDate = now;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            _requestRepository.Update(entity);
            foreach (var history in histories)
                await _historyRepository.AddAsync(history, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return await GetByIdAsync(entity.Id, ct);
    }

    public async Task<RequestDetailDto> UpdateOverallDatesAsync(long id, UpdateRequestOverallDatesDto dto, CancellationToken ct = default)
    {
        var entity = await _requestRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Request {id} was not found.");

        var effectiveStart = dto.OverallStartDate ?? entity.OverallStartDate;
        var effectiveEnd = dto.OverallEndDate ?? entity.OverallEndDate;
        if (effectiveStart.HasValue && effectiveEnd.HasValue && effectiveEnd < effectiveStart)
            throw new BusinessRuleException("Overall end date must not be before the overall start date.");

        var now = Clock.UtcNow;
        var histories = new List<SCIHRequestHistory>();

        if (dto.OverallStartDate.HasValue && dto.OverallStartDate != entity.OverallStartDate)
        {
            histories.Add(NewHistory(entity.Id, SCIHHistoryAction.RequestUpdated,
                $"Overall Start Date changed from '{entity.OverallStartDate:O}' to '{dto.OverallStartDate:O}'.",
                JsonHelper.Serialize(new { overallStartDate = entity.OverallStartDate }),
                JsonHelper.Serialize(new { overallStartDate = dto.OverallStartDate }),
                now));
            entity.OverallStartDate = dto.OverallStartDate;
        }

        if (dto.OverallEndDate.HasValue && dto.OverallEndDate != entity.OverallEndDate)
        {
            histories.Add(NewHistory(entity.Id, SCIHHistoryAction.RequestUpdated,
                $"Overall End Date changed from '{entity.OverallEndDate:O}' to '{dto.OverallEndDate:O}'.",
                JsonHelper.Serialize(new { overallEndDate = entity.OverallEndDate }),
                JsonHelper.Serialize(new { overallEndDate = dto.OverallEndDate }),
                now));
            entity.OverallEndDate = dto.OverallEndDate;
        }

        entity.ModifiedBy = _currentUser.UserId;
        entity.ModifiedDate = now;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            _requestRepository.Update(entity);
            foreach (var history in histories)
                await _historyRepository.AddAsync(history, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return await GetByIdAsync(entity.Id, ct);
    }

    public async Task<PagedResult<RequestListItemDto>> GetPagedAsync(RequestFilterDto filter, CancellationToken ct = default)
    {
        var (items, totalCount) = await _requestRepository.GetPagedAsync(filter, ct);

        return new PagedResult<RequestListItemDto>
        {
            Items = _mapper.Map<List<RequestListItemDto>>(items),
            PageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber,
            PageSize = filter.PageSize < 1 ? 20 : filter.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<RequestDetailDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var entity = await _requestRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Request {id} was not found.");

        var dto = _mapper.Map<RequestDetailDto>(entity);

        var latest = await _historyRepository.GetLatestByRequestIdAsync(id, ct);
        dto.LatestHistory = latest is null ? null : _mapper.Map<RequestHistoryDto>(latest);

        return dto;
    }

    public async Task<List<RequestHistoryDto>> GetHistoryAsync(long id, CancellationToken ct = default)
    {
        if (!await _requestRepository.ExistsIgnoringSoftDeleteAsync(id, ct))
            throw new NotFoundException($"Request {id} was not found.");

        var histories = await _historyRepository.GetByRequestIdAsync(id, ct);
        return _mapper.Map<List<RequestHistoryDto>>(histories);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var entity = await _requestRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Request {id} was not found.");

        var now = Clock.UtcNow;
        entity.IsDeleted = true;
        entity.Status = SCIHStatus.Deleted;
        entity.ModifiedBy = _currentUser.UserId;
        entity.ModifiedDate = now;

        var history = NewHistory(entity.Id, SCIHHistoryAction.RequestDeleted, "Request soft deleted.", null, null, now);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            _requestRepository.Update(entity);
            await _historyRepository.AddAsync(history, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);
    }

    // ---- history helpers ----

    private SCIHRequestHistory NewHistory(long requestId, string action, string? description,
        string? previousValue, string? newValue, DateTime actionDate, string? remarks = null) => new()
    {
        RequestId = requestId,
        Stage = SCIHStage.RequestRaised,
        StageName = SCIHStage.NameOf(SCIHStage.RequestRaised),
        Action = action,
        Description = description,
        PreviousValue = previousValue,
        NewValue = newValue,
        Remarks = remarks,
        ActionBy = _currentUser.UserId,
        ActionDate = actionDate,
        IPAddress = _currentUser.IPAddress,
        DeviceInfo = _currentUser.DeviceInfo
    };

    private void AddFieldChangeHistory(List<SCIHRequestHistory> histories, long requestId,
        string field, string label, string oldValue, string newValue, DateTime now)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal)) return;

        histories.Add(NewHistory(requestId, SCIHHistoryAction.RequestUpdated,
            $"{label} changed from '{oldValue}' to '{newValue}'.",
            JsonHelper.Serialize(new Dictionary<string, string> { [field] = oldValue }),
            JsonHelper.Serialize(new Dictionary<string, string> { [field] = newValue }),
            now));
    }

    private (List<PainPointDto> Merged, List<SCIHRequestHistory> Histories) DiffPainPoints(
        long requestId, List<PainPointDto> oldItems, List<PainPointDto> newItems, DateTime now)
    {
        var histories = new List<SCIHRequestHistory>();
        var nextId = oldItems.Count == 0 ? 1 : oldItems.Max(p => p.Id) + 1;
        var merged = new List<PainPointDto>();
        var seenOldIds = new HashSet<long>();

        foreach (var item in newItems)
        {
            var existing = item.Id > 0 ? oldItems.FirstOrDefault(o => o.Id == item.Id) : null;
            if (existing is not null)
            {
                seenOldIds.Add(existing.Id);
                merged.Add(item);
                if (!JsonEquivalent(JsonHelper.Serialize(existing), JsonHelper.Serialize(item)))
                {
                    histories.Add(NewHistory(requestId, SCIHHistoryAction.PainPointUpdated,
                        $"Pain point '{item.Title}' updated.",
                        JsonHelper.Serialize(existing), JsonHelper.Serialize(item), now));
                }
            }
            else
            {
                var created = new PainPointDto
                {
                    Id = nextId++,
                    Title = item.Title,
                    Description = item.Description,
                    Severity = item.Severity,
                    Frequency = item.Frequency,
                    Remarks = item.Remarks
                };
                merged.Add(created);
                histories.Add(NewHistory(requestId, SCIHHistoryAction.PainPointAdded,
                    $"Pain point '{created.Title}' added.", null, JsonHelper.Serialize(created), now));
            }
        }

        foreach (var removed in oldItems.Where(o => !seenOldIds.Contains(o.Id)))
        {
            histories.Add(NewHistory(requestId, SCIHHistoryAction.PainPointRemoved,
                $"Pain point '{removed.Title}' removed.", JsonHelper.Serialize(removed), null, now));
        }

        return (merged, histories);
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

    private static bool JsonEquivalent(string? a, string? b)
    {
        var nodeA = string.IsNullOrWhiteSpace(a) ? null : JsonNode.Parse(a);
        var nodeB = string.IsNullOrWhiteSpace(b) ? null : JsonNode.Parse(b);
        return JsonNode.DeepEquals(nodeA, nodeB);
    }

    private static List<PainPointDto> AssignIds(List<PainPointDto> items)
    {
        long id = 1;
        return items.Select(i => new PainPointDto
        {
            Id = id++,
            Title = i.Title,
            Description = i.Description,
            Severity = i.Severity,
            Frequency = i.Frequency,
            Remarks = i.Remarks
        }).ToList();
    }

    private List<AttachmentDto> AssignIds(List<AttachmentDto> items, DateTime now)
    {
        long id = 1;
        return items.Select(i =>
        {
            var saved = _attachmentFileService.Persist(i);
            return new AttachmentDto
            {
                Id = id++,
                FileName = saved.FileName,
                OriginalName = saved.OriginalName,
                FileUrl = saved.FileUrl,
                ContentType = saved.ContentType,
                FileSize = saved.FileSize,
                UploadedBy = saved.UploadedBy > 0 ? saved.UploadedBy : _currentUser.UserId,
                UploadedAt = saved.UploadedAt == default ? now : saved.UploadedAt
            };
        }).ToList();
    }
}
