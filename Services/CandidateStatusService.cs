using hrms_api.Data;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Services;

public class CandidateStatusService : ICandidateStatusService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CandidateStatusService> _logger;

    public CandidateStatusService(AppDbContext db, ILogger<CandidateStatusService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task ShortlistCandidateAsync(int candidateId)
    {
        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.CandidateId == candidateId && !c.IsDeleted);

        if (candidate is null)
        {
            _logger.LogWarning("ShortlistCandidate: Candidate {Id} not found", candidateId);
            return;
        }

        var now = DateTime.UtcNow;
        candidate.CurrentStage    = "telephonic";
        candidate.CurrentStatus   = "shortlisted";
        candidate.LastActivityDate = now;
        candidate.UpdatedAt       = now;

        if (candidate.ShortlistingDate is null)
            candidate.ShortlistingDate = now;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Candidate {Id} shortlisted and moved to telephonic stage", candidateId);
    }

    public async Task RejectCandidateAsync(int candidateId)
    {
        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.CandidateId == candidateId && !c.IsDeleted);

        if (candidate is null)
        {
            _logger.LogWarning("RejectCandidate: Candidate {Id} not found", candidateId);
            return;
        }

        var now = DateTime.UtcNow;
        candidate.CurrentStage    = "Rejected";
        candidate.CurrentStatus   = "Rejected";
        candidate.LastActivityDate = now;
        candidate.UpdatedAt       = now;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Candidate {Id} rejected by AI screening", candidateId);
    }

    public async Task MarkPendingReviewAsync(int candidateId, string reason)
    {
        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.CandidateId == candidateId && !c.IsDeleted);

        if (candidate is null)
        {
            _logger.LogWarning("MarkPendingReview: Candidate {Id} not found", candidateId);
            return;
        }

        var now = DateTime.UtcNow;
        candidate.CurrentStatus    = "Pending Review";
        candidate.Remarks          = reason;
        candidate.LastActivityDate = now;
        candidate.UpdatedAt        = now;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Candidate {Id} marked Pending Review: {Reason}", candidateId, reason);
    }
}
