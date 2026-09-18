using Jarvis5.Common;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// Travel CRUD and submission/approval operations. Operational execution is deferred.
/// </summary>
public interface ITravelRequestService
{
    /// <summary>
    /// Create a new Travel Draft.
    ///
    /// Resolves the active "Travel &amp; Hospitality" BusinessModule, then creates a
    /// TravelRequest and its required central EaTask atomically in one transaction.
    /// Travel has no approved TAT classification yet, so the EaTask is created via the
    /// backend-only no-TAT path (AllottedTatMinutes = NULL) — this is fixed module
    /// policy, never a frontend-selectable flag. No EaTaskId = 0 placeholder is ever
    /// persisted.
    /// </summary>
    Task<TravelRequestCreatedDto> CreateDraftAsync(CreateTravelRequestDto dto, CancellationToken ct = default);

    /// <summary>
    /// Get a Travel Request by its database id.
    /// </summary>
    Task<TravelRequestDetailDto> GetByIdAsync(long travelRequestId, CancellationToken ct = default);

    /// <summary>
    /// Edit an unsubmitted draft, or ChangesRequested rework with the current expected cycle.
    /// </summary>
    Task<TravelRequestDetailDto> UpdateDraftAsync(long travelRequestId, UpdateTravelDraftDto dto, CancellationToken ct = default, int? expectedCycleNo = null);

    Task<TravelActionResponseDto> SubmitAsync(long travelRequestId, CancellationToken ct = default);
    Task<TravelActionResponseDto> ApproveAsync(long travelRequestId, ApproveTravelRequestDto dto, CancellationToken ct = default);
    Task<TravelActionResponseDto> RejectAsync(long travelRequestId, RejectTravelRequestDto dto, CancellationToken ct = default);
    Task<TravelActionResponseDto> RequestChangesAsync(long travelRequestId, RequestTravelChangesDto dto, CancellationToken ct = default);
    Task<TravelActionResponseDto> ResubmitAsync(long travelRequestId, ResubmitTravelRequestDto dto, CancellationToken ct = default);

    /// <summary>Upcoming -&gt; Active. Requires ApprovalState Approved (if ApprovalRequired) or NotRequired.</summary>
    Task<TravelActionResponseDto> StartAsync(long travelRequestId, CancellationToken ct = default);
    /// <summary>Active -&gt; Completed. Sets CompletedAt once; never overwrites it.</summary>
    Task<TravelActionResponseDto> CompleteAsync(long travelRequestId, CancellationToken ct = default);
    /// <summary>Any state except Completed/Cancelled -&gt; Cancelled. Does not touch ApprovalState or child records.</summary>
    Task<TravelActionResponseDto> CancelAsync(long travelRequestId, CancellationToken ct = default);

    /// <summary>
    /// Complete chronological Travel timeline (oldest to newest), read from the shared
    /// ea_audit_logs infrastructure. Reading history never writes a new audit record.
    /// </summary>
    Task<List<TravelHistoryEventDto>> GetHistoryAsync(long travelRequestId, CancellationToken ct = default);

    /// <summary>
    /// List/search/filter Travel Requests with pagination.
    /// </summary>
    Task<PagedResult<TravelRequestListItemDto>> ListAsync(TravelRequestListQueryDto query, CancellationToken ct = default);
}
