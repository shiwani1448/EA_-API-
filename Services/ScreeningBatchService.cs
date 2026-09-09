using System.Text.Json;
using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Models;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Services;

public interface IScreeningBatchService
{
    Task<ScreeningStartResponseDto> StartAsync(AiScreeningBatchRequestDto request, CancellationToken cancellationToken = default);
    Task<ScreeningBatchStatusDto?> GetStatusAsync(string batchId, CancellationToken cancellationToken = default);
    Task<ScreeningBatchResultDto?> GetResultAsync(string batchId, CancellationToken cancellationToken = default);
    Task<List<ScreeningDebugCandidateDto>?> GetDebugAsync(string batchId, CancellationToken cancellationToken = default);
    Task ProcessBatchAsync(string batchId, CancellationToken cancellationToken = default);
}

public class ScreeningBatchService : IScreeningBatchService
{
    private static readonly string[] FinalStages = ["Rejected", "Offer", "Joined"];

    private readonly AppDbContext _db;
    private readonly IScreeningBatchQueue _queue;
    private readonly IScreeningResultRepository _repository;
    private readonly IAiScreeningService _aiScreeningService;
    private readonly ILogger<ScreeningBatchService> _logger;

    public ScreeningBatchService(
        AppDbContext db,
        IScreeningBatchQueue queue,
        IScreeningResultRepository repository,
        IAiScreeningService aiScreeningService,
        ILogger<ScreeningBatchService> logger)
    {
        _db = db;
        _queue = queue;
        _repository = repository;
        _aiScreeningService = aiScreeningService;
        _logger = logger;
    }

    public async Task<ScreeningStartResponseDto> StartAsync(
        AiScreeningBatchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var candidateIds = await ResolveCandidateIdsAsync(request, cancellationToken);
        var batch = new ScreeningBatch
        {
            BatchId = Guid.NewGuid().ToString("N"),
            Status = "Queued",
            TotalCandidates = candidateIds.Count,
            ForceRescreen = request.ForceRescreen,
            CandidateIdsJson = JsonSerializer.Serialize(candidateIds),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ScreeningBatches.Add(batch);
        await _db.SaveChangesAsync(cancellationToken);
        await _queue.EnqueueAsync(new ScreeningBatchWorkItem(batch.BatchId), cancellationToken);

        return new ScreeningStartResponseDto
        {
            BatchId = batch.BatchId,
            Status = batch.Status,
            TotalCandidates = batch.TotalCandidates,
            Message = "Screening batch queued. Use the status API to track progress."
        };
    }

    public Task<ScreeningBatchStatusDto?> GetStatusAsync(
        string batchId,
        CancellationToken cancellationToken = default) =>
        _repository.GetStatusAsync(batchId, cancellationToken);

    public Task<ScreeningBatchResultDto?> GetResultAsync(
        string batchId,
        CancellationToken cancellationToken = default) =>
        _repository.GetResultAsync(batchId, cancellationToken);

    public async Task<List<ScreeningDebugCandidateDto>?> GetDebugAsync(
        string batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _repository.GetBatchAsync(batchId, cancellationToken);
        return batch is null ? null : await _repository.GetDebugAsync(batchId, cancellationToken);
    }

    public async Task ProcessBatchAsync(string batchId, CancellationToken cancellationToken = default)
    {
        var batch = await _db.ScreeningBatches.FirstOrDefaultAsync(b => b.BatchId == batchId, cancellationToken);
        if (batch is null)
        {
            _logger.LogWarning("Screening batch {BatchId} not found.", batchId);
            return;
        }

        var candidateIds = DeserializeCandidateIds(batch.CandidateIdsJson);
        batch.Status = candidateIds.Count == 0 ? "Completed" : "Processing";
        batch.StartedAt ??= DateTime.UtcNow;
        batch.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var candidateId in candidateIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await _aiScreeningService.ScreenCandidateAsync(
                    candidateId,
                    batch.ForceRescreen,
                    batch.BatchId,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Candidate {CandidateId} failed in batch {BatchId}", candidateId, batchId);
                await StoreUnexpectedCandidateFailureAsync(batch.BatchId, candidateId, ex, cancellationToken);
            }

            await _repository.RecalculateBatchProgressAsync(batch.BatchId, cancellationToken);
        }

        batch = await _db.ScreeningBatches.FirstAsync(b => b.BatchId == batchId, cancellationToken);
        batch.Status = "Completed";
        batch.CompletedAt = DateTime.UtcNow;
        batch.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<int>> ResolveCandidateIdsAsync(
        AiScreeningBatchRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.CandidateIds is { Count: > 0 })
            return request.CandidateIds.Distinct().ToList();

        var query = _db.Candidates
            .Where(c => !c.IsDeleted
                && c.ResumePath != null && c.ResumePath != ""
                && c.RequisitionId > 0
                && !FinalStages.Contains(c.CurrentStage));

        if (!request.ForceRescreen)
        {
            var alreadyScreenedIds = await _db.CandidateAIScreenings
                .Where(s => s.Status == "Completed")
                .Select(s => s.CandidateId)
                .Distinct()
                .ToListAsync(cancellationToken);

            query = query.Where(c => !alreadyScreenedIds.Contains(c.CandidateId));
        }

        return await query.Select(c => c.CandidateId).ToListAsync(cancellationToken);
    }

    private async Task StoreUnexpectedCandidateFailureAsync(
        string batchId,
        int candidateId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.CandidateId == candidateId, cancellationToken);

        _db.CandidateAIScreenings.Add(new CandidateAIScreening
        {
            BatchId = batchId,
            CandidateId = candidateId,
            CandidateName = candidate?.FullName ?? "Unknown",
            RequisitionId = candidate?.RequisitionId,
            ResumePath = candidate?.ResumePath,
            Status = "Failed",
            ScreeningStatus = "Failed",
            AIStatus = "Skipped",
            FailureReason = $"Failed at Screening: {SafeExceptionMessage(exception)}",
            ErrorMessage = $"Failed at Screening: {SafeExceptionMessage(exception)}",
            CurrentStep = "Screening",
            FailureStep = "Screening",
            ExceptionType = exception.GetType().Name,
            ExceptionMessage = SafeExceptionMessage(exception),
            InnerExceptionMessage = exception.InnerException?.Message,
            JDExtractionStatus = DocumentExtractionConstants.StatusFailed,
            ResumeExtractionStatus = DocumentExtractionConstants.StatusFailed,
            JDExtractionMethod = DocumentExtractionConstants.MethodFailed,
            ResumeExtractionMethod = DocumentExtractionConstants.MethodFailed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            DurationSeconds = 0,
            Decision = "Failed"
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string SafeExceptionMessage(Exception exception) =>
        string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message;

    private static List<int> DeserializeCandidateIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try { return JsonSerializer.Deserialize<List<int>>(json) ?? []; }
        catch { return []; }
    }
}
