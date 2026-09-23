namespace Jarvis5.Common.EaFms;

/// <summary>
/// Delegation-only TAT Rule classification dimension, alongside the existing Type (delegationType).
/// Actual = the doer's original work window (Start -> first Complete). Review = the assignee's
/// decision window for a review cycle (Complete -> Approve/Rework). Rework = the doer's redo
/// window after a rework decision (Rework decision -> next Complete). Every other module leaves
/// TatRule.TaskType null.
/// </summary>
public static class DelegationTaskType
{
    public const string Actual = "Actual", Review = "Review", Rework = "Rework";

    public static bool IsValid(string? value) => value is Actual or Review or Rework;
}
