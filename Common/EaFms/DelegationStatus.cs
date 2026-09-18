namespace Jarvis5.Common.EaFms;

/// <summary>
/// Persisted Delegation execution-lifecycle statuses. DueToday/Overdue are intentionally
/// not represented here — they are time-derived frontend views (not Completed AND
/// DueDate is/before today), computed from Status + DueDate rather than persisted.
/// </summary>
public static class DelegationStatus
{
    public const string Pending = "Pending", InProgress = "InProgress", Completed = "Completed";

    public static bool IsStatus(string? value) => value is Pending or InProgress or Completed;
}
