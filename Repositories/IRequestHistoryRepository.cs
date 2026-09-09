using Jarvis5.Entities;

namespace Jarvis5.Repositories;

public interface IRequestHistoryRepository
{
    Task AddAsync(SCIHRequestHistory history, CancellationToken ct = default);
    Task<List<SCIHRequestHistory>> GetByRequestIdAsync(long requestId, CancellationToken ct = default);
    Task<SCIHRequestHistory?> GetLatestByRequestIdAsync(long requestId, CancellationToken ct = default);
}
