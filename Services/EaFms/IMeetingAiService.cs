using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface IMeetingAiService
{
    /// <summary>Preview-only: analyzes the Meeting's existing completion evidence
    /// (CompletionMom and/or the extracted CompletionPdfAttachmentId text) and returns
    /// AI-proposed action points for EA review. Never writes to the database.</summary>
    Task<MeetingAiAnalysisResponseDto> AnalyzeAsync(long meetingId, CancellationToken ct = default);

    /// <summary>EA confirmation of (possibly edited) AI-proposed actions: creates one real
    /// MeetingAction per submitted entry using the exact same creation logic as the manual
    /// POST /api/ea/meetings/{meetingId}/actions endpoint (MeetingActionFactory), inside one
    /// transaction. Never calls Claude, never creates a Delegation/EaTask/WorkflowInstance,
    /// and never touches Meeting lifecycle/TAT — downstream Delegation conversion continues
    /// to happen only where it already does (Meeting completion), unchanged by this method.</summary>
    Task<MeetingAiActionsConfirmResponseDto> ConfirmActionsAsync(long meetingId, ConfirmMeetingAiActionsRequestDto dto, string actor, CancellationToken ct = default);
}
