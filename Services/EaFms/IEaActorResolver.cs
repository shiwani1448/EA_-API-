namespace Jarvis5.Services.EaFms;

/// <summary>
/// Resolves the frontend-supplied actor identity (the logged-in user's employee code and
/// name from the HRMS login session) to the display name used for operational attribution
/// (CreatedBy/UploadedBy) on EA writes.
///
/// EA APIs do not use JWT authentication, so ICurrentUserService is never populated for
/// EA requests. This resolver is NOT an authentication mechanism and does not replace
/// one — it only validates the session identity the frontend sends. It deliberately does
/// not depend on the HRMS Users table, whose rows do not cover every EA login.
/// </summary>
public interface IEaActorResolver
{
    /// <param name="employeeId">The logged-in user's employee code (e.g. S5I-1048). Blank is rejected.</param>
    /// <param name="employeeName">The logged-in user's name; becomes CreatedBy/UploadedBy. Blank is rejected.</param>
    /// <param name="operationDescription">Short phrase completing "... is required to
    /// {operationDescription}.", e.g. "create a Travel request".</param>
    Task<string> ResolveDisplayNameAsync(string? employeeId, string? employeeName, string operationDescription, CancellationToken ct = default);

    /// <summary>Read-only lookup of a User.Id in the existing Users source; null when it does not exist.</summary>
    Task<EaUserContact?> FindUserContactAsync(int userId, CancellationToken ct = default);
}

/// <summary>Display name and email of an existing User (same FullName formula as GET /api/Users).</summary>
public sealed record EaUserContact(int Id, string FullName, string? Email);
