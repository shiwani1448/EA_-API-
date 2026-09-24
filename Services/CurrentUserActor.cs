using System.Globalization;

namespace Jarvis5.Services;

/// <summary>
/// One place that turns the request's identity into actor values. "0" is never an actor:
/// ActorId/ActorName are null when the token carries no identity; ActorDisplay is only for
/// NOT NULL audit columns (CreatedBy/ModifiedBy) and falls back to "system".
/// </summary>
public static class CurrentUserActor
{
    public const string System = "system";

    public static string? ActorId(this ICurrentUserService user)
    {
        var employeeId = user.EmployeeId;
        if (!string.IsNullOrWhiteSpace(employeeId) && employeeId != "0") return employeeId.Trim();
        return user.UserId > 0 ? user.UserId.ToString(CultureInfo.InvariantCulture) : null;
    }

    public static string? ActorName(this ICurrentUserService user) =>
        string.IsNullOrWhiteSpace(user.UserName) ? null : user.UserName.Trim();

    public static string ActorDisplay(this ICurrentUserService user) =>
        user.ActorName() ?? user.ActorId() ?? System;
}
