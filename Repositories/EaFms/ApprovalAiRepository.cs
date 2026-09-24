using Jarvis5.Data.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories.EaFms;

public class ApprovalAiRepository(EaFmsDbContext db) : IApprovalAiRepository
{
    public async Task<List<(string Approver, int Count)>> GetTopApproversByDepartmentAsync(long excludeApprovalRequestId, string department, CancellationToken ct)
    {
        var rows = await db.ApprovalRequests.AsNoTracking()
            .Where(a => !a.IsDeleted && a.Id != excludeApprovalRequestId && a.Department == department
                && a.WorkflowStatus == "Approved" && a.ApprovedBy != null)
            .GroupBy(a => a.ApprovedBy!)
            .Select(g => new { Approver = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(5)
            .ToListAsync(ct);

        return rows.Select(r => (r.Approver, r.Count)).ToList();
    }
}
