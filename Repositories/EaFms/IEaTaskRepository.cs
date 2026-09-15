using Jarvis5.Entities.EaFms;

namespace Jarvis5.Repositories.EaFms;

public interface IEaTaskRepository
{
    IQueryable<EaTask> Query();
    Task AddAsync(EaTask task, CancellationToken ct);
}
