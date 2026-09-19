namespace Jarvis5.Services.EaFms;

/// <summary>
/// Resolves a frontend-supplied stable actor identity (User.Id from the existing HRMS
/// Users source — the same id GET /api/Users already returns) to that user's display
/// name, for operational attribution (CreatedBy/UploadedBy) on EA writes.
///
/// EA APIs do not use JWT authentication, so ICurrentUserService is never populated for
/// EA requests. This resolver is NOT an authentication mechanism and does not replace
/// one — it only turns an id the frontend already has into a display name the backend
/// controls, so the frontend can never submit an arbitrary name directly.
/// </summary>
public interface IEaActorResolver
{
    /// <param name="userId">The frontend-supplied User.Id. Null/zero/negative and
    /// unknown ids are rejected.</param>
    /// <param name="operationDescription">Short phrase completing "... is required to
    /// {operationDescription}.", e.g. "create a Travel request".</param>
    Task<string> ResolveDisplayNameAsync(int? userId, string operationDescription, CancellationToken ct = default);

    /// <summary>Read-only lookup of a User.Id in the existing Users source; null when it does not exist.</summary>
    Task<EaUserContact?> FindUserContactAsync(int userId, CancellationToken ct = default);
}

/// <summary>Display name and email of an existing User (same FullName formula as GET /api/Users).</summary>
public sealed record EaUserContact(int Id, string FullName, string? Email);
