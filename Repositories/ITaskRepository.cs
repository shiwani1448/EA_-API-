using Jarvis5.Entities;

namespace Jarvis5.Repositories;

public interface ITaskRepository
{
    Task<SCIHTask?> GetByIdAsync(long taskId, CancellationToken ct = default);

    /// <summary>All non-deleted modules for a request, oldest first.</summary>
    Task<List<SCIHTask>> GetByRequestIdAsync(long requestId, CancellationToken ct = default);

    Task AddAsync(SCIHTask task, CancellationToken ct = default);
    void Update(SCIHTask task);
}
