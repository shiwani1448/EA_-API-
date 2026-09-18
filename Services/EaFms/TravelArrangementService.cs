using System.Globalization;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public partial class TravelArrangementService : ITravelArrangementService
{
    private readonly EaFmsDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IAuditService _audit;
    public TravelArrangementService(EaFmsDbContext db, ICurrentUserService user, IAuditService audit)
    { _db = db; _user = user; _audit = audit; }
    private string Actor => _user.UserName ?? _user.UserId.ToString(CultureInfo.InvariantCulture);

    private static void EnsureReady(TravelRequest parent)
    {
        // Same currently supported operational state as Step 6, without changing bookings.
        if (parent.BusinessState != "Upcoming" || (parent.ApprovalRequired
            ? parent.ApprovalState != "Approved" : parent.ApprovalState != "NotRequired"))
            throw new BusinessRuleException("Travel request is not ready for operational arrangements.");
    }

    private async Task<TravelRequest> ParentAsync(long id, bool locked, CancellationToken ct)
    {
        TravelRequest? parent;
        if (locked && _db.Database.IsRelational())
        {
            // Parent-first lock convention shared with existing Travel writers.
            var rows = await _db.TravelRequests.FromSqlInterpolated(
                $"SELECT * FROM public.ea_travel_requests WHERE \"Id\" = {id} AND NOT \"IsDeleted\" FOR UPDATE").ToListAsync(ct);
            parent = rows.SingleOrDefault();
            if (parent != null) await _db.Entry(parent).ReloadAsync(ct);
        }
        else parent = await _db.TravelRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        return parent ?? throw new NotFoundException($"Travel request {id} not found.");
    }

    private static void ValidateStatus(string? status)
    {
        if (status is not (null or "Requested" or "Scheduled" or "InProgress" or "Completed" or "Cancelled"))
            throw new BadRequestException("Unsupported arrangement status.");
    }
    private static void ValidateCost(decimal? cost)
    {
        if (cost is < 0 or > 9999999999999999.99m || (cost.HasValue && decimal.Round(cost.Value, 2) != cost))
            throw new BadRequestException("Cost must be nonnegative, fit numeric(18,2), and have at most two decimal places.");
    }
}
