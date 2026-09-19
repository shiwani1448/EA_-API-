using Jarvis5.Common;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// Frontend-supplied actor snapshot (employeeId + employeeName) used for EA operational
/// attribution. It is NOT authenticated or verified by the EA backend: the values are only
/// trimmed, length-bounded and stored. Nothing is looked up in Users, Employees or HRMS.
/// </summary>
public sealed record EaActorSnapshot(string? EmployeeId, string? EmployeeName)
{
    public const int MaxLength = 100;

    public static EaActorSnapshot None { get; } = new(null, null);

    /// <summary>Best single display value for the legacy CreatedBy/ModifiedBy string columns.</summary>
    public string? DisplayName => EmployeeName ?? EmployeeId;

    public static EaActorSnapshot From(string? employeeId, string? employeeName)
    {
        var id = Clean(employeeId, "employeeId");
        var name = Clean(employeeName, "employeeName");
        return id is null && name is null ? None : new EaActorSnapshot(id, name);
    }

    private static string? Clean(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength) throw new BadRequestException($"{field} must not exceed {MaxLength} characters.");
        return trimmed;
    }
}
