using Jarvis5.Entities;

namespace Jarvis5.Repositories;

public interface ISnagListRepository
{
    Task<SCIHSnagList?> GetByIdAsync(long snagId, CancellationToken ct = default);

    /// <summary>All non-deleted Snag Lists, oldest first — used to find matches by
    /// doer id, which lives inside each stage's StageDetails JSON and so can't be
    /// filtered in SQL.</summary>
    Task<List<SCIHSnagList>> GetAllAsync(CancellationToken ct = default);

    Task AddAsync(SCIHSnagList snag, CancellationToken ct = default);
    void Update(SCIHSnagList snag);
}
