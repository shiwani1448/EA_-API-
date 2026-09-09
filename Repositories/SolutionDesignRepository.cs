using Jarvis5.Data;
using Jarvis5.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories;

public class SolutionDesignRepository : ISolutionDesignRepository
{
    private readonly AppDbContext _context;

    public SolutionDesignRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<SCIHSolutionDesign?> GetByRequestIdAsync(long requestId, CancellationToken ct = default) =>
        _context.SolutionDesigns.FirstOrDefaultAsync(d => d.RequestId == requestId, ct);

    public async Task<Dictionary<long, SCIHSolutionDesign>> GetByRequestIdsAsync(IReadOnlyCollection<long> requestIds, CancellationToken ct = default)
    {
        if (requestIds.Count == 0) return new Dictionary<long, SCIHSolutionDesign>();

        return await _context.SolutionDesigns
            .AsNoTracking()
            .Where(d => requestIds.Contains(d.RequestId))
            .ToDictionaryAsync(d => d.RequestId, ct);
    }

    public async Task AddAsync(SCIHSolutionDesign design, CancellationToken ct = default) =>
        await _context.SolutionDesigns.AddAsync(design, ct);

    public void Update(SCIHSolutionDesign design) => _context.SolutionDesigns.Update(design);
}
