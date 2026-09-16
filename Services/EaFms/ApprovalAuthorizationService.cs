using Jarvis5.Data.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public class ApprovalAuthorizationService : IApprovalAuthorizationService
{
    private readonly EaFmsDbContext _db;
    public ApprovalAuthorizationService(EaFmsDbContext db)
    {
        _db = db;
    }

    public async Task<bool> CanPerformAsync(long approvalRequestId, string operation, CancellationToken ct = default)
    {
        var request = await _db.ApprovalRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == approvalRequestId && !r.IsDeleted, ct);
        if (request is null) return false;

        if (!await _db.Tasks.AsNoTracking().AnyAsync(t => t.Id == request.EaTaskId && t.IsActive && !t.IsDeleted, ct))
            return false;

        // EA Approval is intentionally unauthenticated. Existence and active linked-task
        // validation remain centralized here; no caller identity is evaluated.
        return true;
    }
}
