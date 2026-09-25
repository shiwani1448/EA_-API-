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

    public async Task<EaUserContact?> FindUserContactAsync(int userId, CancellationToken ct = default)
    {
        var user = await _hrmsDb.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .FirstOrDefaultAsync(ct);
        return user is null ? null : new EaUserContact(user.Id, (user.FirstName + " " + user.LastName).Trim(), user.Email);
    }

    // CreatedBy/UploadedBy columns are varchar(100).
    private const int MaxActorNameLength = 100;

    public Task<string> ResolveDisplayNameAsync(string? employeeId, string? employeeName, string operationDescription, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            throw new BusinessRuleException($"A valid employeeId is required to {operationDescription}.");
        var name = employeeName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleException($"A valid employeeName is required to {operationDescription}.");
        if (name.Length > MaxActorNameLength)
            throw new BusinessRuleException($"employeeName must not exceed {MaxActorNameLength} characters.");
        return Task.FromResult(name);
    }
}
