using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// The single place MeetingAction rows get built from a CreateMeetingActionDto, shared by
/// the manual POST /api/ea/meetings/{meetingId}/actions endpoint and the AI confirmation
/// endpoint (POST .../ai/actions/confirm), so both produce identical rows under identical
/// field-mapping rules. DoerId/Priority are trimmed-or-null; everything else is stored
/// verbatim. DoerId is NEVER derived from DoerName here or anywhere else — it is only ever
/// what the caller explicitly supplied.
/// </summary>
public static class MeetingActionFactory
{
    public static MeetingAction Build(long meetingId, CreateMeetingActionDto dto, string createdBy, DateTime createdDate) => new()
    {
        MeetingId = meetingId,
        Title = dto.Title,
        Description = dto.Description,
        DoerId = string.IsNullOrWhiteSpace(dto.DoerId) ? null : dto.DoerId.Trim(),
        DoerName = dto.DoerName,
        Priority = string.IsNullOrWhiteSpace(dto.Priority) ? null : dto.Priority.Trim(),
        DueDate = dto.DueDate,
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
        Status = a.Status,
        IsOverdue = a.CompletedAt == null && a.DueDate != null && a.DueDate < now,
    };
}
