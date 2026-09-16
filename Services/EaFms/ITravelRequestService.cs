using Jarvis5.Common;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// Travel Request CRUD/read operations.
/// Submit, approve, reject, rework and lifecycle actions are NOT in this interface
/// (those belong to Step 4+).
/// </summary>
public interface ITravelRequestService
{
    /// <summary>
    /// Create a new Travel Draft.
    ///
    /// ⚠ BLOCKED — TRAVEL EATASK/TAT CREATION POLICY REQUIRES DECISION
    ///
    /// The current EaTaskService enforces: "Task creation without TAT is only supported
    /// for EA Approval." Travel has no TAT rule configured and is not EA Approval,
    /// so neither CreateAsync nor CreateWithoutTatAsync can legally be called.
    ///
    /// This method generates a ReferenceNo and persists the TravelRequest with a
    /// placeholder EaTaskId = 0 to satisfy the non-nullable FK at the EF model level.
    /// The actual EaTask MUST be created after the TAT/task-creation policy for Travel
    /// is approved and implemented.
    ///
    /// The response clearly signals this state via the returned dto.
    /// </summary>
    Task<TravelRequestCreatedDto> CreateDraftAsync(CreateTravelRequestDto dto, CancellationToken ct = default);

    /// <summary>
    /// Get a Travel Request by its database id.
    /// </summary>
    Task<TravelRequestDetailDto> GetByIdAsync(long travelRequestId, CancellationToken ct = default);

    /// <summary>
    /// Update a Travel Draft. Only allowed while BusinessState == "Draft".
    /// </summary>
    Task<TravelRequestDetailDto> UpdateDraftAsync(long travelRequestId, UpdateTravelDraftDto dto, CancellationToken ct = default);

    /// <summary>
    /// List/search/filter Travel Requests with pagination.
    /// </summary>
    Task<PagedResult<TravelRequestListItemDto>> ListAsync(TravelRequestListQueryDto query, CancellationToken ct = default);
}
