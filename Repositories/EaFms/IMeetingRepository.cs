using Jarvis5.Entities.EaFms;

namespace Jarvis5.Repositories.EaFms;

public interface IMeetingRepository
{
    Task AddAsync(Meeting meeting, CancellationToken ct = default);
    Task<Meeting?> GetByIdAsync(long id, CancellationToken ct = default);
    Task UpdateAsync(Meeting meeting);
    Task<List<Meeting>> QueryAsync(Func<IQueryable<Meeting>, IQueryable<Meeting>>? query = null, CancellationToken ct = default);
}
