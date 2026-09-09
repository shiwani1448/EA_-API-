using Jarvis5.Entities.EaFms;

namespace Jarvis5.Repositories.EaFms;

public interface IEscalationRepository
{
    Task AddAsync(Escalation escalation, CancellationToken ct = default);
    Task<Escalation?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<List<Escalation>> GetByFollowupIdAsync(long followupId, CancellationToken ct = default);
    void Update(Escalation escalation);
    Task<List<EscalationLevel>> GetActiveLevelsAsync(CancellationToken ct = default);
}
