using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// The single place MeetingAction rows get built from a CreateMeetingActionDto, shared by
/// the manual POST /api/ea/meetings/{meetingId}/actions endpoint and the AI confirmation
/// endpoint (POST .../ai/actions/confirm), so both produce identical rows under identical
/// field-mapping rules. DoerId/Priority/AssigneeId/AssigneeName/DelegationType are
/// trimmed-or-null; everything else is stored verbatim. DoerId is NEVER derived from
/// DoerName here or anywhere else — it is only ever what the caller explicitly supplied.
/// </summary>
public static class MeetingActionFactory
{
    /// <summary>
    /// Validates the Delegation-bound fields before an action is saved. A DelegationType is
    /// carried into the Delegation created on Meeting completion, and a typed Delegation
    /// requires exactly one active (Type, Actual) Delegation TAT rule — so an unusable type is
    /// rejected now (409) instead of failing the whole Meeting completion later.
    /// </summary>
    public static async Task ValidateAsync(EaFmsDbContext db, CreateMeetingActionDto dto, CancellationToken ct)
    {
        if (dto.AssigneeId?.Trim().Length > 100) throw new BadRequestException("AssigneeId must not exceed 100 characters.");
        if (dto.AssigneeName?.Trim().Length > 200) throw new BadRequestException("AssigneeName must not exceed 200 characters.");
        var type = NullIfBlank(dto.DelegationType);
        if (type is null) return;
        if (type.Length > 200) throw new BadRequestException("DelegationType must not exceed 200 characters.");

        var delegationModuleId = await db.BusinessModules.AsNoTracking()
            .Where(m => !m.IsDeleted && m.IsActive && m.Name == DelegationService.DelegationBusinessModuleName)
            .Select(m => (long?)m.Id).FirstOrDefaultAsync(ct);
        if (delegationModuleId is null)
            throw new BusinessRuleException($"An active '{DelegationService.DelegationBusinessModuleName}' business module is required to use a delegationType.");

        var rules = await new TatRuleRepository(db).GetApplicableByTypeOnlyAsync(delegationModuleId.Value, type, DelegationTaskType.Actual, ct);
        if (rules.Count == 0)
            throw new BusinessRuleException($"No active Delegation TAT rule is configured for delegationType '{type}'. Configure one or leave delegationType empty.");
        if (rules.Count > 1)
            throw new BusinessRuleException($"Multiple active Delegation TAT rules are configured for delegationType '{type}'.");
    }

    public static MeetingAction Build(long meetingId, CreateMeetingActionDto dto, string createdBy, DateTime createdDate) => new()
    {
        MeetingId = meetingId,
        Title = dto.Title,
        Description = dto.Description,
        DoerId = string.IsNullOrWhiteSpace(dto.DoerId) ? null : dto.DoerId.Trim(),
        DoerName = dto.DoerName,
        Priority = string.IsNullOrWhiteSpace(dto.Priority) ? null : dto.Priority.Trim(),
        DueDate = dto.DueDate,
        StartDate = dto.StartDate,
        AssigneeId = NullIfBlank(dto.AssigneeId),
        AssigneeName = NullIfBlank(dto.AssigneeName),
        DelegationType = NullIfBlank(dto.DelegationType),
        Status = dto.Status,
        CreatedBy = createdBy,
        CreatedDate = createdDate,
        IsDeleted = false,
    };

    public static MeetingActionDto ToDto(MeetingAction a, DateTime now) => new()
    {
        Id = a.Id,
        Title = a.Title,
        Description = a.Description,
        DoerId = a.DoerId,
        DoerName = a.DoerName,
        Priority = a.Priority,
        DueDate = a.DueDate,
        StartDate = a.StartDate,
        AssigneeId = a.AssigneeId,
        AssigneeName = a.AssigneeName,
        DelegationType = a.DelegationType,
        Status = a.Status,
        IsOverdue = a.CompletedAt == null && a.DueDate != null && a.DueDate < now,
    };

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
