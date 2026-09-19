using Jarvis5.Common;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public class EaActorResolver : IEaActorResolver
{
    private readonly hrms_api.Data.AppDbContext _hrmsDb;

    public EaActorResolver(hrms_api.Data.AppDbContext hrmsDb)
    {
        _hrmsDb = hrmsDb;
    }

    public async Task<string> ResolveDisplayNameAsync(int? userId, string operationDescription, CancellationToken ct = default)
    {
        if (!userId.HasValue || userId.Value <= 0)
            throw new BusinessRuleException($"A valid userId is required to {operationDescription}.");

        // Same FullName composition GET /api/Users already uses (UsersController.GetAll) —
        // one canonical formula, not a second copy that could drift from it.
        var user = await _hrmsDb.Users.AsNoTracking()
            .Where(u => u.Id == userId.Value)
            .Select(u => new { u.FirstName, u.LastName })
            .FirstOrDefaultAsync(ct);
        if (user is null)
            throw new BusinessRuleException($"User {userId.Value} does not exist.");

        var fullName = (user.FirstName + " " + user.LastName).Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            throw new BusinessRuleException($"User {userId.Value} has no resolvable name.");

        return fullName;
    }
}
