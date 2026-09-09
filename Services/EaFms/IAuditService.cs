using System.Threading;
using System.Threading.Tasks;

namespace Jarvis5.Services.EaFms;

public interface IAuditService
{
    /// <summary>
    /// Create an AuditLog entity and add it to the provided EaFmsDbContext instance (no SaveChanges).
    /// Caller is responsible for ensuring the audit entry is added to the same DbContext/transaction
    /// as the related business changes.
    /// </summary>
    void AddAuditLog(Jarvis5.Entities.EaFms.AuditLog log);

    /// <summary>
    /// Convenience helper to create and add an audit record from data objects.
    /// Does not call SaveChanges.
    /// </summary>
    void AddAudit(
        string actionType,
        string module,
        string entityName,
        string entityId,
        object? oldValues,
        object? newValues,
        string? description = null);
}
