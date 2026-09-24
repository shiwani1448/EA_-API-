using System.Text.Json;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services;

namespace Jarvis5.Services.EaFms;

public class AuditService : IAuditService
{
    private readonly EaFmsDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public AuditService(EaFmsDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public void AddAuditLog(AuditLog log)
    {
        if (log is null) return;
        // Ensure OccurredAt/CreatedDate
        var now = Clock.UtcNowTz;
        log.OccurredAt = log.OccurredAt == default ? now : log.OccurredAt;
        log.CreatedDate = log.CreatedDate == default ? now : log.CreatedDate;
        log.CreatedBy = string.IsNullOrWhiteSpace(log.CreatedBy)
            ? _currentUser.ActorDisplay()
            : log.CreatedBy;
        _context.AuditLogs.Add(log);
    }

    public void AddAudit(string actionType, string module, string entityName, string entityId, object? oldValues, object? newValues, string? description = null)
    {
        var actorId = _currentUser.ActorId();
        var actorName = _currentUser.ActorName();

        var log = new AuditLog
        {
            ActorId = actorId,
            ActorName = actorName,
            ActionType = actionType,
            Module = module,
            EntityName = entityName,
            EntityId = entityId,
            Description = description,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues, _jsonOptions),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues, _jsonOptions),
            OccurredAt = Clock.UtcNowTz,
            CreatedBy = actorName ?? actorId ?? "system",
            CreatedDate = Clock.UtcNowTz
        };

        _context.AuditLogs.Add(log);
    }
}
