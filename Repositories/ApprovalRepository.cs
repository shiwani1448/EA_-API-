using Jarvis5.Data;
using Jarvis5.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories;

public class ApprovalRepository : IApprovalRepository
{
    private readonly AppDbContext _context;

    public ApprovalRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<SCIHApproval?> GetLatestByRequestIdAsync(long requestId, CancellationToken ct = default) =>
        _context.Approvals
            .Where(a => a.RequestId == requestId)
            .OrderByDescending(a => a.ApprovalRound)
            .FirstOrDefaultAsync(ct);

    public Task<List<SCIHApproval>> GetAllByRequestIdAsync(long requestId, CancellationToken ct = default) =>
        _context.Approvals
            .AsNoTracking()
            .Where(a => a.RequestId == requestId)
            .OrderBy(a => a.ApprovalRound)
            .ToListAsync(ct);

    public async Task AddAsync(SCIHApproval approval, CancellationToken ct = default) =>
        await _context.Approvals.AddAsync(approval, ct);

    public void Update(SCIHApproval approval) => _context.Approvals.Update(approval);
}
