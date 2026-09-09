namespace Jarvis5.Entities.EaFms;

/// <summary>
/// AuditLog: record of important events/actions within EA FMS (create, update, delete, status change, escalation, access).
/// Stores actor info, action type, target module/entity id, timestamps and optional payloads.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    // Actor performing the action (user id or name)
    // Store both an id and display name when available for flexibility.
    public string? ActorId { get; set; }
    public string? ActorName { get; set; }

    // Action type (e.g. Create, Update, Delete, StatusChange, Approval, Escalation, Access)
    public string ActionType { get; set; } = string.Empty;

    // Module / entity name (e.g. "IntakeRequest")
    public string Module { get; set; } = string.Empty;

    // Entity name (optional) and identifier of the entity instance affected
    public string? EntityName { get; set; }
    public string EntityId { get; set; } = string.Empty;

    // Optional human-readable description
    public string? Description { get; set; }

    // Optional old/new values (JSON or text) to capture state changes
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }

    public DateTime OccurredAt { get; set; }

    // Audit metadata (who recorded the audit entry in the system)
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
