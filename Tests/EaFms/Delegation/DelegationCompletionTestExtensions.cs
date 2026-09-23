using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Http;

namespace Jarvis5.Tests.EaFms.Delegation;

/// <summary>
/// Test-only convenience for the Phase 1 Task Review change to Delegation's completion contract:
/// CompleteAsync no longer finishes the Delegation by itself — it opens a review cycle, and
/// ApproveReviewAsync is what actually finalizes it (see DelegationService.CompleteAsync's own doc
/// comment). Tests written against the old "Complete finishes the Delegation" contract call this
/// instead of CompleteAsync directly to reach the same end state, keeping every other assertion
/// (TAT freezing, pause-anchor closing, attachment handling, atomicity, etc.) unchanged.
/// </summary>
internal static class DelegationCompletionTestExtensions
{
    public static async Task<DelegationResponseDto> CompleteAndApproveAsync(
        this DelegationService svc, long delegationId, IFormFile? completionPdf = null, CancellationToken ct = default)
    {
        await svc.CompleteAsync(delegationId, completionPdf, ct);
        return await svc.ApproveReviewAsync(delegationId, new ApproveTaskReviewRequestDto(), null, ct);
    }
}
