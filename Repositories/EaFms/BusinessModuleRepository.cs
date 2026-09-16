using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories.EaFms;

public sealed class BusinessModuleRepository(EaFmsDbContext db) : IBusinessModuleRepository
{
    public IQueryable<BusinessModule> Query(bool activeOnly = false)
    {
        var query = db.BusinessModules.AsNoTracking().Where(x => !x.IsDeleted);
        return activeOnly ? query.Where(x => x.IsActive) : query;
    }

    public Task<BusinessModule?> GetForUpdateAsync(long id, CancellationToken ct) =>
        db.BusinessModules.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);

    public Task<bool> ExistsNormalizedNameAsync(string normalizedName, long? excludingId, CancellationToken ct) =>
        db.BusinessModules.AnyAsync(x => !x.IsDeleted
            && x.Name.Trim().ToLower() == normalizedName
            && (!excludingId.HasValue || x.Id != excludingId.Value), ct);

    public Task AddAsync(BusinessModule module, CancellationToken ct) => db.BusinessModules.AddAsync(module, ct).AsTask();
}
