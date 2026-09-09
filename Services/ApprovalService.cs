using Jarvis5.Common;
using Jarvis5.Dtos.Analysis;
using Jarvis5.Dtos.Approval;
using Jarvis5.Dtos.SolutionDesign;
using Jarvis5.Entities;
using Jarvis5.Repositories;

namespace Jarvis5.Services;

public class ApprovalService : IApprovalService
{
    private readonly IRequestRepository _requestRepository;
    private readonly IAnalysisRepository _analysisRepository;
    private readonly ISolutionDesignRepository _solutionDesignRepository;
    private readonly IApprovalRepository _approvalRepository;
    private readonly IRequestHistoryRepository _historyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public ApprovalService(
        IRequestRepository requestRepository,
        IAnalysisRepository analysisRepository,
        ISolutionDesignRepository solutionDesignRepository,
        IApprovalRepository approvalRepository,
        IRequestHistoryRepository historyRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _requestRepository = requestRepository;
        _analysisRepository = analysisRepository;
        _solutionDesignRepository = solutionDesignRepository;
        _approvalRepository = approvalRepository;
        _historyRepository = historyRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApprovalDetailDto> SubmitForApprovalAsync(long requestId, CancellationToken ct = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException($"Request {requestId} was not found.");

        if (request.CurrentStage != SCIHStage.SolutionDesign)
        {
            throw new BusinessRuleException(
                $"This request is at stage '{SCIHStage.NameOf(request.CurrentStage)}'; the Solution Design must be approved before it can be submitted for Director approval.");
        }

        var latest = await _approvalRepository.GetLatestByRequestIdAsync(requestId, ct);
        var now = Clock.UtcNow;
        var previousStage = request.CurrentStage;

        var round = new SCIHApproval
        {
            RequestId = requestId,
            ApprovalRound = (latest?.ApprovalRound ?? 0) + 1,
            ReworkCount = latest?.ReworkCount ?? 0,
            Decision = SCIHApprovalDecision.Pending,
            CreatedDate = now
        };

        request.CurrentStage = SCIHStage.Approval;
        request.Status = SCIHStatus.PendingApproval;
        request.ModifiedBy = _currentUser.UserId;
        request.ModifiedDate = now;

        var histories = new List<SCIHRequestHistory>
        {
            NewHistory(requestId, SCIHHistoryAction.SubmittedForApproval,
                $"Submitted for Director approval (round {round.ApprovalRound}).", null, null, now),
            NewHistory(requestId, SCIHHistoryAction.StageChanged,
                $"Stage changed from '{SCIHStage.NameOf(previousStage)}' to '{SCIHStage.NameOf(SCIHStage.Approval)}'.",
                JsonHelper.Serialize(new { stage = previousStage, stageName = SCIHStage.NameOf(previousStage) }),
                JsonHelper.Serialize(new { stage = SCIHStage.Approval, stageName = SCIHStage.NameOf(SCIHStage.Approval) }),
                now)
        };

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _approvalRepository.AddAsync(round, ct);
            _requestRepository.Update(request);
            foreach (var h in histories)
                await _historyRepository.AddAsync(h, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return await GetDetailAsync(requestId, ct);
    }

    public async Task<ApprovalDetailDto> ApproveAsync(long requestId, ApproveRequestDto dto, CancellationToken ct = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException($"Request {requestId} was not found.");

        var round = await EnsurePendingRoundAsync(request, ct);

        var now = Clock.UtcNow;
        var previousStage = request.CurrentStage;

        round.Decision = SCIHApprovalDecision.Approved;
        round.ApprovedBy = dto.ApprovedBy;
        round.ApprovedDate = now;
        round.Comments = dto.Comments;

        request.CurrentStage = SCIHStage.Development;
        request.Status = SCIHStatus.Development;
        request.ModifiedBy = _currentUser.UserId;
        request.ModifiedDate = now;

        var histories = new List<SCIHRequestHistory>
        {
            NewHistory(requestId, SCIHHistoryAction.RequestApproved,
                $"Request approved (round {round.ApprovalRound}).", null,
                JsonHelper.Serialize(new { approvedBy = round.ApprovedBy, comments = round.Comments }), now),
            NewHistory(requestId, SCIHHistoryAction.StageChanged,
                $"Stage changed from '{SCIHStage.NameOf(previousStage)}' to '{SCIHStage.NameOf(SCIHStage.Development)}'.",
                JsonHelper.Serialize(new { stage = previousStage, stageName = SCIHStage.NameOf(previousStage) }),
                JsonHelper.Serialize(new { stage = SCIHStage.Development, stageName = SCIHStage.NameOf(SCIHStage.Development) }),
                now)
        };

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            _approvalRepository.Update(round);
            _requestRepository.Update(request);
            foreach (var h in histories)
                await _historyRepository.AddAsync(h, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return await GetDetailAsync(requestId, ct);
    }

    public async Task<ApprovalDetailDto> RejectAsync(long requestId, RejectRequestDto dto, CancellationToken ct = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException($"Request {requestId} was not found.");

        var round = await EnsurePendingRoundAsync(request, ct);

        var now = Clock.UtcNow;
        var previousStage = request.CurrentStage;

        round.Decision = SCIHApprovalDecision.Rejected;
        round.ApprovedBy = dto.ApprovedBy;
        round.ApprovedDate = now;
        round.Comments = dto.Comments;
        round.RejectionReason = dto.RejectionReason;
        round.ImprovementAreasJson = JsonHelper.Serialize(dto.ImprovementAreas);
        round.ReworkCount += 1;

        request.CurrentStage = SCIHStage.Analysis;
        request.Status = SCIHStatus.AnalysisRework;
        request.ModifiedBy = _currentUser.UserId;
        request.ModifiedDate = now;

        // Solution Design locks itself once Approved. Unlock it back to Reviewed so the
        // next rework cycle (Analysis rework -> Solution Design edit/regenerate -> resubmit)
        // isn't permanently blocked by that earlier approval.
        var solutionDesign = await _solutionDesignRepository.GetByRequestIdAsync(requestId, ct);
        if (solutionDesign is not null && solutionDesign.Status == SCIHSolutionDesignStatus.Approved)
        {
            solutionDesign.Status = SCIHSolutionDesignStatus.Reviewed;
            solutionDesign.ModifiedDate = now;
        }

        var histories = new List<SCIHRequestHistory>
        {
            NewHistory(requestId, SCIHHistoryAction.RequestRejected,
                $"Request rejected (round {round.ApprovalRound}); rework count is now {round.ReworkCount}.", null,
                JsonHelper.Serialize(new
                {
                    approvedBy = round.ApprovedBy,
                    rejectionReason = round.RejectionReason,
                    improvementAreas = dto.ImprovementAreas,
                    comments = round.Comments,
                    reworkCount = round.ReworkCount
                }), now),
            NewHistory(requestId, SCIHHistoryAction.StageChanged,
                $"Stage changed from '{SCIHStage.NameOf(previousStage)}' to '{SCIHStage.NameOf(SCIHStage.Analysis)}' for rework.",
                JsonHelper.Serialize(new { stage = previousStage, stageName = SCIHStage.NameOf(previousStage) }),
                JsonHelper.Serialize(new { stage = SCIHStage.Analysis, stageName = SCIHStage.NameOf(SCIHStage.Analysis) }),
                now)
        };

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            _approvalRepository.Update(round);
            _requestRepository.Update(request);
            if (solutionDesign is not null)
                _solutionDesignRepository.Update(solutionDesign);
            foreach (var h in histories)
                await _historyRepository.AddAsync(h, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return await GetDetailAsync(requestId, ct);
    }

    public async Task<ApprovalDetailDto> GetDetailAsync(long requestId, CancellationToken ct = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException($"Request {requestId} was not found.");

        var analysis = await _analysisRepository.GetByRequestIdAsync(requestId, ct);
        var solutionDesign = await _solutionDesignRepository.GetByRequestIdAsync(requestId, ct);
        var rounds = await _approvalRepository.GetAllByRequestIdAsync(requestId, ct);

        return new ApprovalDetailDto
        {
            RequestId = request.Id,
            RequestNo = request.RequestNo,
            Title = request.Title,
            DepartmentId = request.DepartmentId,
            RaisedBy = request.RaisedBy,
            CurrentStage = request.CurrentStage,
            CurrentStageName = SCIHStage.NameOf(request.CurrentStage),
            RequestStatus = request.Status,
            Analysis = analysis is null ? null : ToAnalysisSummary(analysis),
            SolutionDesign = solutionDesign is null ? null : ToSolutionDesignSummary(solutionDesign),
            CurrentRound = rounds.Count == 0 ? null : ToRoundDto(rounds[^1]),
            PreviousRound = rounds.Count < 2 ? null : ToRoundDto(rounds[^2])
        };
    }

    public async Task<List<ApprovalRoundDto>> GetRoundsAsync(long requestId, CancellationToken ct = default)
    {
        var exists = await _requestRepository.ExistsIgnoringSoftDeleteAsync(requestId, ct);
        if (!exists) throw new NotFoundException($"Request {requestId} was not found.");

        var rounds = await _approvalRepository.GetAllByRequestIdAsync(requestId, ct);
        return rounds.Select(ToRoundDto).ToList();
    }

    private async Task<SCIHApproval> EnsurePendingRoundAsync(SCIHRequest request, CancellationToken ct)
    {
        if (request.CurrentStage != SCIHStage.Approval)
        {
            throw new BusinessRuleException(
                $"This request is at stage '{SCIHStage.NameOf(request.CurrentStage)}'; it has not been submitted for approval.");
        }

        var round = await _approvalRepository.GetLatestByRequestIdAsync(request.Id, ct)
            ?? throw new BusinessRuleException("This request has not been submitted for approval yet.");

        if (round.Decision != SCIHApprovalDecision.Pending)
            throw new BusinessRuleException($"Approval round {round.ApprovalRound} has already been decided ('{round.Decision}').");

        return round;
    }

    private static AnalysisSummaryDto ToAnalysisSummary(SCIHAnalysis analysis) => new()
    {
        Id = analysis.Id,
        Version = analysis.Version,
        ExecutiveSummary = JsonHelper.DeserializeObjectOrDefault<AiAnalysisResultDto>(analysis.AnalysisJson).ExecutiveSummary,
        CreatedDate = analysis.CreatedDate
    };

    private static SolutionDesignSummaryDto ToSolutionDesignSummary(SCIHSolutionDesign design)
    {
        var solution = JsonHelper.DeserializeObjectOrDefault<SolutionDesignResultDto>(design.SolutionJson);
        return new SolutionDesignSummaryDto
        {
            Id = design.Id,
            Version = design.Version,
            Status = design.Status,
            Title = solution.SolutionOverview.Title,
            ExecutiveSummary = solution.ExecutiveSummary,
            CreatedDate = design.CreatedDate
        };
    }

    private static ApprovalRoundDto ToRoundDto(SCIHApproval a) => new()
    {
        Id = a.Id,
        ApprovalRound = a.ApprovalRound,
        ReworkCount = a.ReworkCount,
        Decision = a.Decision,
        Comments = a.Comments,
        RejectionReason = a.RejectionReason,
        ImprovementAreas = JsonHelper.DeserializeList<string>(a.ImprovementAreasJson),
        ApprovedBy = a.ApprovedBy,
        ApprovedDate = a.ApprovedDate,
        CreatedDate = a.CreatedDate
    };

    private SCIHRequestHistory NewHistory(long requestId, string action, string? description,
        string? previousValue, string? newValue, DateTime actionDate) => new()
    {
        RequestId = requestId,
        Stage = SCIHStage.Approval,
        StageName = SCIHStage.NameOf(SCIHStage.Approval),
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
