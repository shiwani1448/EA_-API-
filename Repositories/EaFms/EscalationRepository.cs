using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories.EaFms;

public class EscalationRepository : IEscalationRepository
{
    private readonly EaFmsDbContext _context;

    public EscalationRepository(EaFmsDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Escalation escalation, CancellationToken ct = default) =>
        await _context.Escalations.AddAsync(escalation, ct);

    public Task<Escalation?> GetByIdAsync(long id, CancellationToken ct = default) =>
        _context.Escalations.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);

    public Task<List<Escalation>> GetByFollowupIdAsync(long followupId, CancellationToken ct = default) =>
        _context.Escalations
            .AsNoTracking()
            .Where(e => e.FollowupId == followupId && !e.IsDeleted)
            .OrderBy(e => e.InitiatedAt)
            .ToListAsync(ct);

    public void Update(Escalation escalation) => _context.Escalations.Update(escalation);

    public Task<List<EscalationLevel>> GetActiveLevelsAsync(CancellationToken ct = default) =>
        _context.EscalationLevels
            .AsNoTracking()
            .Where(l => !l.IsDeleted)
            .OrderBy(l => l.Level)
            .ToListAsync(ct);
}
