using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface IFollowupSourceResolver
{
    Task<long> GetMeetingModuleIdAsync(CancellationToken ct = default);
    Task<FollowupSourceResponseDto> ResolveAsync(long? businessModuleId, string? businessRecordId, CancellationToken ct = default);

    /// <summary>
    /// Validates that BusinessModuleId is an active module and that BusinessRecordId is a real
    /// source record for it, returning the canonical record id. Throws NotFoundException for an
    /// unknown module/record and BadRequestException for a malformed record id. Modules are matched
    /// by catalog name (never by numeric id); a module without a dedicated source lookup is
    /// validated against its central EaTask instead.
    /// </summary>
    Task<FollowupSourceRecord> ValidateAsync(long businessModuleId, string businessRecordId, CancellationToken ct = default);
}

/// <summary>A validated follow-up source. Intake/Workflow ids are the source record's own linkage, when it has one.</summary>
public sealed record FollowupSourceRecord(
    string ModuleName, string BusinessRecordId, string? Title, long? IntakeRequestId, long? WorkflowInstanceId);
