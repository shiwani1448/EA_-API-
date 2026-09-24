namespace Jarvis5.Repositories.EaFms;

public interface IFollowupAiRepository
{
    /// <summary>CreatedDate/CompletedAt pairs for every other completed Followup of the
    /// given free-text Type — the raw samples an average resolution time is computed from.
    /// The only honest source of a "how long does this kind of follow-up usually take"
    /// signal, since this system has no SLA/turnaround configuration for Followups.</summary>
    Task<List<(DateTime CreatedDate, DateTime CompletedAt)>> GetCompletedDurationSamplesByTypeAsync(long excludeFollowupId, string type, CancellationToken ct);
}
