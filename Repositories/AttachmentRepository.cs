using Jarvis5.Data;
using Jarvis5.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories;

public class AttachmentRepository : IAttachmentRepository
{
    private readonly AppDbContext _context;

    public AttachmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SCIHAttachment attachment, CancellationToken ct = default) =>
        await _context.Attachments.AddAsync(attachment, ct);

    public Task<List<SCIHAttachment>> GetByEntityAsync(string entityType, long entityId, CancellationToken ct = default) =>
        _context.Attachments
            .AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderBy(a => a.UploadedAt)
            .ToListAsync(ct);

    public Task<SCIHAttachment?> GetByIdAsync(long id, CancellationToken ct = default) =>
        _context.Attachments.FirstOrDefaultAsync(a => a.Id == id, ct);

    public void Update(SCIHAttachment attachment) => _context.Attachments.Update(attachment);
}
