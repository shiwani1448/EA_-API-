using hrms_api.Data;
using hrms_api.Models;
using System.Text.Json;

namespace hrms_api.Services;

public class ActivityService : IActivityService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ActivityService> _logger;

    public ActivityService(AppDbContext db, ILogger<ActivityService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task AddAiScreeningActivityAsync(int candidateId, string decision, decimal? score, string? remarks)
    {
        var stage = decision switch
        {
            "Shortlist"      => "Shortlisted",
            "Reject"         => "Rejected",
            _                => "Pending Review"
        };

        var evalPayload = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            aiDecision    = decision,
            overallScore  = score,
            screeningType = "AIScreening"
        }));

        var activity = new CandidateActivity
        {
            CandidateId  = candidateId,
            ActivityType = "AIScreening",
            Stage        = stage,
            Status       = stage,
            TotalScore   = score.HasValue ? (int)score.Value : null,
            EvaluationJson = evalPayload,
            Remarks      = remarks,
            ActionDate   = DateTime.UtcNow,
            CreatedAt    = DateTime.UtcNow
        };

        _db.CandidateActivities.Add(activity);
        await _db.SaveChangesAsync();

        _logger.LogInformation("AI screening activity added for candidate {Id}: {Decision}", candidateId, decision);
    }
}
