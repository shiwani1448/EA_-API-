using Jarvis5.Entities;

namespace Jarvis5.Repositories;

public interface IAttachmentRepository
{
    Task AddAsync(SCIHAttachment attachment, CancellationToken ct = default);

    /// <summary>All non-deleted attachments for a given module, oldest first.</summary>
    Task<List<SCIHAttachment>> GetByEntityAsync(string entityType, long entityId, CancellationToken ct = default);

    /// <summary>Tracked lookup by id — used by Delete/Download, which both need the
    /// entity attached so an update (or a physical-file read) can follow.</summary>
    Task<SCIHAttachment?> GetByIdAsync(long id, CancellationToken ct = default);

    void Update(SCIHAttachment attachment);
}
