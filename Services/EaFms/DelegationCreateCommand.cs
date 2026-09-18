namespace Jarvis5.Services.EaFms;

/// <summary>
/// Internal, transaction-composable Delegation creation input — used by
/// DelegationService.CreateCoreAsync. Deliberately NOT the same type as the public HTTP
/// DelegationCreateRequestDto: this is the internal service contract for any caller
/// (the public create path today; a future source module such as Meeting/Travel/Approval
/// later), so the two can evolve independently and no caller needs to construct or depend
/// on frontend HTTP DTO semantics. Carries no actor/transaction/status fields — those
/// remain exclusively server-owned inside CreateCoreAsync itself.
/// </summary>
public sealed class DelegationCreateCommand
{
    public string? Title { get; init; }
    public string? Description { get; init; }

    public string? AssignedToId { get; init; }
    public string? AssignedToNameSnapshot { get; init; }

    public string? Priority { get; init; }
    public DateTime? DueDate { get; init; }

    public long SourceBusinessModuleId { get; init; }
    public string? SourceEntityId { get; init; }
    public string? SourceReference { get; init; }

    public string? AdditionalNotes { get; init; }
}
