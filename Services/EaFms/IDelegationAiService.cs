using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface IDelegationAiService
{
    /// <summary>Preview-only: suggests a doer purely from historical Delegations of the same
    /// DelegationType — there is no employee/role directory, so this can never name anyone
    /// who wasn't a real doer of a past delegation of the same type, and returns null when
    /// there is no history to draw from. Never writes to the database.</summary>
    Task<DelegationAiOwnerSuggestionResponseDto> SuggestOwnerAsync(long delegationId, CancellationToken ct = default);

    /// <summary>Writes the given doer (the EA's reviewed/edited choice — never re-derived
    /// from Claude) onto the real delegation via a read-modify-write through the existing
    /// IDelegationService.UpdateAsync (a full-replace endpoint, so every other editable
    /// field is first copied from the current record unchanged). 409 once Completed, same
    /// rule UpdateAsync itself already enforces.</summary>
    Task<DelegationResponseDto> ApplySuggestedOwnerAsync(long delegationId, ApplySuggestedOwnerRequestDto dto, CancellationToken ct = default);

    /// <summary>Preview-only: suggests a due date computed from a configured TAT rule for
    /// this DelegationType, or failing that the real average completion time of past
    /// completed delegations of the same type. The date is always computed in code, never
    /// by Claude. Never writes to the database.</summary>
    Task<DelegationAiDueDatePredictionResponseDto> PredictDueDateAsync(long delegationId, CancellationToken ct = default);

    /// <summary>Writes the given due date (the EA's reviewed/edited choice — never re-derived
    /// from Claude) onto the real delegation's EndDate, via the same read-modify-write
    /// pattern as ApplySuggestedOwnerAsync. 409 once Completed.</summary>
    Task<DelegationResponseDto> ApplyPredictedDueDateAsync(long delegationId, ApplyPredictedDueDateRequestDto dto, CancellationToken ct = default);

    /// <summary>Preview-only: assesses delay risk from the Delegation's own real execution
    /// data (status, due-date proximity, TAT usage, pause history). Never writes to the
    /// database.</summary>
    Task<DelegationAiDelayRiskResponseDto> CheckDelayRiskAsync(long delegationId, CancellationToken ct = default);
}
