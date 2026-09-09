using Jarvis5.Common;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;

namespace Jarvis5.Repositories.EaFms;

public interface IFollowupRepository
{
    Task AddAsync(Followup followup, CancellationToken ct = default);
    Task<Followup?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<List<Followup>> GetByIntakeRequestIdAsync(long intakeRequestId, CancellationToken ct = default);
    Task<List<Followup>> GetOpenFollowupsAsync(CancellationToken ct = default);
    Task<PagedResult<Followup>> GetPagedAsync(FollowupListQueryDto query, DateTime now, CancellationToken ct = default);
    Task<List<Followup>> GetBySourceAsync(long moduleId, string recordId, CancellationToken ct = default);
    void Update(Followup followup);
}
