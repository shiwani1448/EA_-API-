using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface IApprovalAiService
{
    /// <summary>Preview-only: judges completeness from the request's own fields and its
    /// documents' file names only (no OCR/content access). Never writes to the database.</summary>
    Task<ApprovalAiReadinessResponseDto> CheckReadinessAsync(long approvalRequestId, CancellationToken ct = default);

    /// <summary>Preview-only: suggests an approver purely from historical Approved requests
    /// in the same department — there is no employee/role directory, so this can never name
    /// anyone who wasn't a real approver of a past request, and returns null when there is
    /// no history to draw from. Never writes to the database.</summary>
    Task<ApprovalAiApproverSuggestionResponseDto> RecommendApproverAsync(long approvalRequestId, CancellationToken ct = default);

    /// <summary>Preview-only: plain-English narrative built only from this request's own
    /// cycles/history/due-state. Never writes to the database.</summary>
    Task<ApprovalAiStatusSummaryResponseDto> SummarizeStatusAsync(long approvalRequestId, CancellationToken ct = default);
}
