using Jarvis5.Entities;

namespace Jarvis5.Repositories;

public interface ITaskHistoryRepository
{
    Task AddAsync(SCIHTaskHistory history, CancellationToken ct = default);
    Task<List<SCIHTaskHistory>> GetByTaskIdAsync(long taskId, CancellationToken ct = default);
    Task<List<SCIHTaskHistory>> GetByRequestIdAsync(long requestId, CancellationToken ct = default);
}
