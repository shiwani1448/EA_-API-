using System.Globalization;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// Read-only Director review. ApproverId remains opaque: no supported mapping to
/// HRMS User.Id/EmployeeId exists. This follows current unscoped EA access behavior.
/// </summary>
public class TravelApprovalQueryService(
    EaFmsDbContext db, ITravelRequestService requests, ITravelDocumentService documents)
    : ITravelApprovalQueryService
{
    public async Task<PagedResult<TravelPendingApprovalDto>> PendingAsync(
        TravelPendingApprovalQueryDto query, CancellationToken ct = default)
    {
        // TravelRequest has no IsActive field. Nondeleted + current Pending cycle
        // defines an active approval. Historical pending cycles cannot qualify a row.
        var term = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim().ToLower();
        var pending = from request in db.TravelRequests.AsNoTracking()
                      join cycle in db.TravelRequestCycles.AsNoTracking()
                      on new { RequestId = request.Id, CycleNo = request.CurrentCycleNo }
                      equals new { RequestId = cycle.TravelRequestId, cycle.CycleNo }
                      where !request.IsDeleted && request.ApprovalState == "Pending"
                          && request.CurrentCycleNo > 0 && cycle.DecisionState == "Pending"
                          && (term == null
                              || request.ReferenceNo.ToLower().Contains(term)
                              || request.Travellers.Any(t => t.TravellerName != null && t.TravellerName.ToLower().Contains(term))
                              || (request.FromLocation != null && request.FromLocation.ToLower().Contains(term))
                              || (request.ToLocation != null && request.ToLocation.ToLower().Contains(term))
                              || (request.Purpose != null && request.Purpose.ToLower().Contains(term)))
                      select new TravelPendingApprovalDto
                      {
                          TravelRequestId = request.Id, ReferenceNo = request.ReferenceNo,
                          Purpose = request.Purpose, FromLocation = request.FromLocation,
                          ToLocation = request.ToLocation, DepartureDate = request.DepartureDate,
                          ReturnDate = request.ReturnDate, RequiredDate = request.RequiredDate,
                          Priority = request.Priority, BusinessState = request.BusinessState,
                          ApprovalState = request.ApprovalState, CurrentCycleNo = request.CurrentCycleNo,
                          SubmittedAt = cycle.SubmittedAt, ApproverId = cycle.ApproverId,
                          ApproverName = cycle.ApproverNameSnapshot,
                          TotalEstimatedCost = (request.EstimatedTravelCost ?? 0m)
                              + (request.EstimatedHotelCost ?? 0m)
                              + (request.EstimatedLocalTransportCost ?? 0m)
                              + (request.EstimatedHospitalityCost ?? 0m),
                          Currency = request.Currency
                      };
        var page = Math.Max(1, query.Page);
        var pageSize = query.PageSize < 1 ? 50 : Math.Min(200, query.PageSize);

        var count = await pending.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var items = offset >= count ? new List<TravelPendingApprovalDto>()
            : await pending.OrderByDescending(x => x.SubmittedAt).ThenByDescending(x => x.TravelRequestId)
                .Skip((int)offset).Take(pageSize).ToListAsync(ct);
        if (items.Count > 0)
        {
            var requestIds = items.Select(x => x.TravelRequestId).ToArray();
            var travellers = (await db.TravelTravellers.AsNoTracking()
                .Where(t => requestIds.Contains(t.TravelRequestId))
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
                .ToListAsync(ct)).ToLookup(t => t.TravelRequestId);
            foreach (var item in items)
                item.Travellers = travellers[item.TravelRequestId].Select(t => new TravelTravellerDto
                {
                    TravellerName = t.TravellerName, EmployeePersonId = t.EmployeePersonId,
                    Department = t.Department, ContactInformation = t.ContactInformation
                }).ToList();
            var ids = items.Select(x => x.TravelRequestId.ToString(CultureInfo.InvariantCulture)).ToArray();
            var counts = await db.Attachments.AsNoTracking()
                .Where(a => a.RelatedModule == "Travel" && a.RelatedEntity == "TravelRequest"
                    && a.RelatedEntityId != null && ids.Contains(a.RelatedEntityId) && a.IsActive && !a.IsDeleted)
                .GroupBy(a => a.RelatedEntityId!)
                .Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
            foreach (var item in items)
                item.DocumentCount = counts.GetValueOrDefault(item.TravelRequestId.ToString(CultureInfo.InvariantCulture));
        }
        return new() { Items = items, PageNumber = page, PageSize = pageSize, TotalCount = count };
    }

    public async Task<TravelApprovalDetailDto> GetAsync(long travelRequestId, CancellationToken ct = default)
    {
        var request = await requests.GetByIdAsync(travelRequestId, ct);
        var history = await db.TravelRequestCycles.AsNoTracking()
            .Where(c => c.TravelRequestId == travelRequestId).OrderBy(c => c.CycleNo)
            .Select(c => new TravelCurrentCycleDto
            {
                CycleNo = c.CycleNo, DecisionState = c.DecisionState ?? string.Empty,
                SubmittedBy = c.SubmittedBy, SubmittedAt = c.SubmittedAt,
                ApproverId = c.ApproverId, ApproverNameSnapshot = c.ApproverNameSnapshot,
                ChangeReason = c.ChangeReason, ChangesMade = c.ChangesMade,
                DecisionComment = c.DecisionComment, DecisionBy = c.DecisionBy, DecisionAt = c.DecisionAt
            }).ToListAsync(ct);
        return new()
        {
            TravelRequestId = request.Id, ReferenceNo = request.ReferenceNo,
            BusinessState = request.BusinessState, ApprovalState = request.ApprovalState,
            CurrentCycleNo = request.CurrentCycleNo, SubmittedAt = request.SubmittedAt,
            ApprovedAt = request.ApprovedAt, RejectedAt = request.RejectedAt,
            Travellers = request.Travellers, Trip = request.Trip, Transportation = request.Transportation,
            Hotel = request.Hotel, LocalTransport = request.LocalTransport, Hospitality = request.Hospitality,
            Itinerary = request.Itinerary, Budget = request.Budget, Approval = request.Approval,
            CurrentCycle = history.SingleOrDefault(c => c.CycleNo == request.CurrentCycleNo),
            ApprovalHistory = history, Documents = await documents.ListAsync(travelRequestId, ct)
        };
    }
}
