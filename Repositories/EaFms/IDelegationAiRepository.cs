namespace Jarvis5.Repositories.EaFms;

public interface IDelegationAiRepository
{
    /// <summary>Top 5 doers (by count) among past delegations of the given type, excluding
    /// the delegation currently being reasoned about, with each doer's most recently used
    /// display name. The only honest source of a "who usually does this kind of thing"
    /// signal, since this system has no employee/role directory.</summary>
    Task<List<(string DoerId, string? DoerName, int Count)>> GetTopDoersByTypeAsync(long excludeDelegationId, string delegationType, CancellationToken ct);

    /// <summary>Started/Completed timestamp pairs for every other Completed delegation of
    /// the given type — the raw samples an average turnaround is computed from.</summary>
    Task<List<(DateTime StartedAt, DateTime CompletedAt)>> GetCompletedDurationSamplesAsync(long excludeDelegationId, string delegationType, CancellationToken ct);

    /// <summary>The active "Delegation" BusinessModule's id, or null if it isn't configured.</summary>
    Task<long?> ResolveDelegationModuleIdAsync(CancellationToken ct);
}
