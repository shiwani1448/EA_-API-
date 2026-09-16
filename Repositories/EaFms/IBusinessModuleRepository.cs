using Jarvis5.Entities.EaFms;

namespace Jarvis5.Repositories.EaFms;

public interface IBusinessModuleRepository
{
    IQueryable<BusinessModule> Query(bool activeOnly = false);
    Task<BusinessModule?> GetForUpdateAsync(long id, CancellationToken ct);
    Task<bool> ExistsNormalizedNameAsync(string normalizedName, long? excludingId, CancellationToken ct);
    Task AddAsync(BusinessModule module, CancellationToken ct);
}
