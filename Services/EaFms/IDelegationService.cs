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
    /// Doer's "I'm done" action. Despite the name this no longer finalizes the Delegation directly —
    /// it uploads the optional completion PDF (stored in ea_attachments in the same transaction, so a
    /// failure rolls back the whole call) and opens a Task Review cycle (see ApproveReviewAsync/
    /// RequestReworkAsync below). The Delegation stays InProgress until the assignee approves it.
    /// 409 if not currently InProgress, or if a review cycle is already pending.
    /// </summary>
    Task<DelegationResponseDto> CompleteAsync(long delegationId, IFormFile? completionPdf, CancellationToken ct = default);

    /// <summary>
    /// Starts the reviewer's SLA clock for the currently open Review phase, which Complete/
    /// SubmitForReview now open idle (StartedAt null) instead of live. 404 unknown id; 409 if not
    /// InProgress, paused, there's no open phase, the open phase isn't Review, or it was already started.
    /// </summary>
    Task<DelegationResponseDto> StartReviewAsync(long delegationId, CancellationToken ct = default);
    /// <summary>
    /// Starts the doer's redo clock for the currently open Rework phase, which review/rework now opens
    /// idle (StartedAt null) instead of live. 404 unknown id; 409 if not InProgress, paused, there's no
    /// open phase, the open phase isn't Rework, or it was already started.
    /// </summary>
    Task<DelegationResponseDto> StartReworkAsync(long delegationId, CancellationToken ct = default);
    /// <summary>
    /// Opens exactly one shared WorkPause for an InProgress Delegation (status stays InProgress; isPaused=true).
    /// 409 when not InProgress, already paused, or the currently open phase has not been started yet
    /// (StartReviewAsync/StartReworkAsync/StartAsync must run first — pausing a phase whose clock
    /// never started doesn't make sense).
    /// </summary>
    Task<DelegationResponseDto> PauseAsync(long delegationId, DelegationPauseRequestDto? request, CancellationToken ct = default);
    /// <summary>Closes the open WorkPause of an InProgress Delegation (isPaused=false). 409 when not paused.</summary>
    Task<DelegationResponseDto> ResumeAsync(long delegationId, CancellationToken ct = default);

    /// <summary>
    /// Standalone Task Review/Rework submit, independent of CompleteAsync's own internal call to the
    /// same shared ITaskReviewService method (e.g. flagging InProgress work for an early look).
    /// </summary>
    Task<DelegationResponseDto> SubmitForReviewAsync(long delegationId, SubmitForReviewRequestDto dto, CancellationToken ct = default);
    /// <summary>
    /// Requires the latest review cycle to be PendingReview. 409 otherwise (including double-approve).
    /// Unlike the shared engine's own Approve, this one finalizes the Delegation as Completed too —
    /// CompleteAsync above only opened the review cycle; this is what actually closes it out.
    /// <paramref name="attachment"/> is optional: the assignee's own document for this decision,
    /// stored in ea_attachments and surfaced back as reviewSummary.attachmentId.
    /// </summary>
    Task<DelegationResponseDto> ApproveReviewAsync(long delegationId, ApproveTaskReviewRequestDto dto, IFormFile? attachment, CancellationToken ct = default);
    /// <summary>
    /// Requires the latest review cycle to be PendingReview. Does not create the next cycle (the next
    /// CompleteAsync/SubmitForReview does) and leaves the Delegation exactly as it was — still
    /// InProgress, since CompleteAsync never marked it Completed in the first place.
    /// <paramref name="attachment"/> is optional: the assignee's own document for this decision,
    /// stored in ea_attachments and surfaced back as reviewSummary.attachmentId.
    /// </summary>
    Task<DelegationResponseDto> RequestReworkAsync(long delegationId, RequestTaskReworkRequestDto dto, IFormFile? attachment, CancellationToken ct = default);
    /// <summary>Full review-cycle history, oldest (cycle 1) first.</summary>
    Task<List<TaskReviewHistoryItemDto>> GetReviewHistoryAsync(long delegationId, CancellationToken ct = default);
}
