using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Models;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Services;

public interface IScreeningResultRepository
{
    Task<ScreeningBatch?> GetBatchAsync(string batchId, CancellationToken cancellationToken = default);
    Task<ScreeningBatchStatusDto?> GetStatusAsync(string batchId, CancellationToken cancellationToken = default);
    Task<ScreeningBatchResultDto?> GetResultAsync(string batchId, CancellationToken cancellationToken = default);
    Task<List<ScreeningDebugCandidateDto>> GetDebugAsync(string batchId, CancellationToken cancellationToken = default);
    Task RecalculateBatchProgressAsync(string batchId, CancellationToken cancellationToken = default);
}

public class ScreeningResultRepository : IScreeningResultRepository
{
    private readonly AppDbContext _db;

    public ScreeningResultRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<ScreeningBatch?> GetBatchAsync(string batchId, CancellationToken cancellationToken = default) =>
        _db.ScreeningBatches.FirstOrDefaultAsync(b => b.BatchId == batchId, cancellationToken);

    public async Task<ScreeningBatchStatusDto?> GetStatusAsync(
        string batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await GetBatchAsync(batchId, cancellationToken);
        return batch is null ? null : MapStatus(batch);
    }

    public async Task<ScreeningBatchResultDto?> GetResultAsync(
        string batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await GetBatchAsync(batchId, cancellationToken);
        if (batch is null)
            return null;

        var dto = new ScreeningBatchResultDto
        {
            BatchId = batch.BatchId,
            Status = batch.Status,
            TotalCandidates = batch.TotalCandidates,
            Completed = batch.Completed,
            Failed = batch.Failed,
            PendingReview = batch.PendingReview,
            Shortlisted = batch.Shortlisted,
            Rejected = batch.Rejected,
            FailureReason = batch.FailureReason,
            CreatedAt = batch.CreatedAt,
            StartedAt = batch.StartedAt,
            CompletedAt = batch.CompletedAt,
            UpdatedAt = batch.UpdatedAt
        };

        dto.Results = await _db.CandidateAIScreenings
            .Where(s => s.BatchId == batchId)
            .OrderBy(s => s.StartedAt)
            .ThenBy(s => s.CandidateId)
            .Select(s => new ScreeningCandidateResultDto
            {
                CandidateId = s.CandidateId,
                CandidateName = s.CandidateName ?? string.Empty,
                JDTextAvailable = s.JDTextAvailable,
                ResumeTextAvailable = s.ResumeTextAvailable,
                JDExtractionMethod = s.JDExtractionMethod,
                ResumeExtractionMethod = s.ResumeExtractionMethod,
                JDExtractionStatus = s.JDExtractionStatus,
                ResumeExtractionStatus = s.ResumeExtractionStatus,
                ScreeningStatus = s.ScreeningStatus ?? s.Status,
                AIStatus = s.AIStatus,
                Score = s.OverallScore,
                Decision = s.Decision,
                Recommendation = s.Recommendation,
                FailureReason = s.FailureReason ?? s.ErrorMessage,
                NormalTextLength = s.NormalTextLength,
                PdfPageCount = s.PdfPageCount,
                RenderedImageCreated = s.RenderedImageCreated,
                RenderedImagePath = s.RenderedImagePath,
                OCRTextLength = s.OCRTextLength,
                ExtractionMethodUsed = s.ExtractionMethodUsed,
                CurrentStep = s.CurrentStep,
                FailureStep = s.FailureStep,
                ExceptionType = s.ExceptionType,
                ExceptionMessage = s.ExceptionMessage,
                InnerExceptionMessage = s.InnerExceptionMessage,
                AiPromptLength = s.AiPromptLength,
                AiResponseLength = s.AiResponseLength,
                DurationSeconds = s.DurationSeconds,
                StartedAt = s.StartedAt,
                CompletedAt = s.CompletedAt
            })
            .ToListAsync(cancellationToken);

        return dto;
    }

    public Task<List<ScreeningDebugCandidateDto>> GetDebugAsync(
        string batchId,
        CancellationToken cancellationToken = default) =>
        _db.CandidateAIScreenings
            .Where(s => s.BatchId == batchId)
            .OrderBy(s => s.StartedAt)
            .ThenBy(s => s.CandidateId)
            .Select(s => new ScreeningDebugCandidateDto
            {
                CandidateId = s.CandidateId,
                CandidateName = s.CandidateName ?? string.Empty,
                CurrentStep = s.CurrentStep,
                FailureStep = s.FailureStep,
                ExceptionType = s.ExceptionType,
                ExceptionMessage = s.ExceptionMessage,
                NormalTextLength = s.NormalTextLength,
                OcrTextLength = s.OCRTextLength,
                AiPromptLength = s.AiPromptLength,
                AiResponseLength = s.AiResponseLength,
                DurationSeconds = s.DurationSeconds
            })
            .ToListAsync(cancellationToken);

    public async Task RecalculateBatchProgressAsync(string batchId, CancellationToken cancellationToken = default)
    {
        var batch = await GetBatchAsync(batchId, cancellationToken);
        if (batch is null)
            return;

        var results = await _db.CandidateAIScreenings
            .Where(s => s.BatchId == batchId)
            .Select(s => new { s.Status, s.Decision })
            .ToListAsync(cancellationToken);

        batch.Completed = results.Count;
        batch.Failed = results.Count(r => r.Status == "Failed");
        batch.PendingReview = results.Count(r => r.Status == "Pending Review");
        batch.Shortlisted = results.Count(r => r.Decision == "Shortlist");
        batch.Rejected = results.Count(r => r.Decision == "Reject");
        batch.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static ScreeningBatchStatusDto MapStatus(ScreeningBatch batch) => new()
    {
        BatchId = batch.BatchId,
        Status = batch.Status,
        TotalCandidates = batch.TotalCandidates,
        Completed = batch.Completed,
        Failed = batch.Failed,
        PendingReview = batch.PendingReview,
        Shortlisted = batch.Shortlisted,
        Rejected = batch.Rejected,
        FailureReason = batch.FailureReason,
        CreatedAt = batch.CreatedAt,
        StartedAt = batch.StartedAt,
        CompletedAt = batch.CompletedAt,
        UpdatedAt = batch.UpdatedAt
    };
}
