using Jarvis5.Common;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.AspNetCore.Http;

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
    /// <summary>
    /// InProgress -> Completed. Synchronizes the linked EaTask atomically. 409 if not currently InProgress.
    /// <paramref name="completionPdf"/> is optional: when supplied it must be a valid PDF and is stored in
    /// ea_attachments inside the same transaction, so a failure rolls back the whole completion.
    /// </summary>
    Task<DelegationResponseDto> CompleteAsync(long delegationId, IFormFile? completionPdf, CancellationToken ct = default);
    /// <summary>
    /// Opens exactly one shared WorkPause for an InProgress Delegation (status stays InProgress; isPaused=true).
    /// 409 when not InProgress or already paused.
    /// </summary>
    Task<DelegationResponseDto> PauseAsync(long delegationId, DelegationPauseRequestDto? request, CancellationToken ct = default);
    /// <summary>Closes the open WorkPause of an InProgress Delegation (isPaused=false). 409 when not paused.</summary>
    Task<DelegationResponseDto> ResumeAsync(long delegationId, CancellationToken ct = default);
}
