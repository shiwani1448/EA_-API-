using Jarvis5.Data.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public class ApprovalAuthorizationService : IApprovalAuthorizationService
{
    private readonly EaFmsDbContext _db;
    private readonly ICurrentUserService _user;

    public ApprovalAuthorizationService(EaFmsDbContext db, ICurrentUserService user)
    {
        _db = db; _user = user;
    }

    public async Task<bool> CanPerformAsync(long approvalRequestId, string operation, CancellationToken ct = default)
    {
        var request = await _db.ApprovalRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == approvalRequestId && !r.IsDeleted, ct);
        if (request is null) return false;

        var userId = _user.UserId;
        var userName = _user.UserName;

        // Allow if user is the creator (CreatedBy stores username)
        if (!string.IsNullOrWhiteSpace(request.CreatedBy) && string.Equals(request.CreatedBy, userName, StringComparison.OrdinalIgnoreCase))
            return true;

        // Prefer stable identity: ApproverId if present (stores stable user id string). If ApproverId matches current user id, allow.
        if (!string.IsNullOrWhiteSpace(request.ApproverId))
        {
            if (long.TryParse(request.ApproverId, out var approverId) && approverId == userId) return true;
            if (string.Equals(request.ApproverId, userName, StringComparison.OrdinalIgnoreCase)) return true;
        }

        // No global role mapping present in repository; deny otherwise.
        return false;
    }
}
