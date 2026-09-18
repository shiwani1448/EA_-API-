using Jarvis5.Common;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// Delegation CRUD/register/lifecycle operations (Steps 2-4). History/Reminder/
/// Escalation belong to later steps and are not present here.
/// </summary>
public interface IDelegationService
{
    Task<DelegationResponseDto> CreateAsync(DelegationCreateRequestDto dto, CancellationToken ct = default);
    Task<DelegationResponseDto> GetByIdAsync(long delegationId, CancellationToken ct = default);
    Task<DelegationResponseDto> UpdateAsync(long delegationId, DelegationUpdateRequestDto dto, CancellationToken ct = default);
    Task<PagedResult<DelegationResponseDto>> ListAsync(DelegationListQueryDto query, CancellationToken ct = default);
    /// <summary>Register KPI card counts (total/pending/inProgress/dueToday/overdue/completed).</summary>
    Task<DelegationSummaryResponseDto> GetSummaryAsync(CancellationToken ct = default);
    /// <summary>Pending -> InProgress. Synchronizes the linked EaTask atomically. 409 if not currently Pending.</summary>
    Task<DelegationResponseDto> StartAsync(long delegationId, CancellationToken ct = default);
    /// <summary>InProgress -> Completed. Synchronizes the linked EaTask atomically. 409 if not currently InProgress.</summary>
    Task<DelegationResponseDto> CompleteAsync(long delegationId, CancellationToken ct = default);
}
