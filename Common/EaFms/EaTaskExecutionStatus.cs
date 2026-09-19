namespace Jarvis5.Common.EaFms;

/// <summary>
/// Canonical, deliberately small cross-module task-execution vocabulary persisted on
/// EaTask.ExecutionStatus. This represents CENTRAL TASK EXECUTION only — whether actual
/// work on the task has begun/finished — and is never a substitute for a module's own
/// business status (Meeting.statusName, Travel.BusinessState, Approval.WorkflowStatus,
/// Delegation.Status all remain separate and unchanged).
/// </summary>
public static class EaTaskExecutionStatus
{
    public const string NotStarted = "NotStarted";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    private static readonly HashSet<string> All = new(StringComparer.Ordinal)
    {
        NotStarted, InProgress, Completed, Cancelled
    };

    public static bool IsValid(string value) => All.Contains(value);

    public static IReadOnlyCollection<string> Values => All;

    /// <summary>Returns the canonical spelling of a status (case-insensitive), or null if unknown.</summary>
    public static string? Canonicalize(string value) =>
        All.FirstOrDefault(s => string.Equals(s, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}
