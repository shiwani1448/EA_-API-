using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Jarvis5.Services.EaFms;

public class NotificationService : INotificationService
{
    private readonly EaFmsDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public NotificationService(EaFmsDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private string GetCurrentRecipientId()
    {
        // Use canonical representation: currentUser.UserId as invariant string
        return _currentUser.UserId.ToString(CultureInfo.InvariantCulture);
    }

    public async Task<NotificationResponseDto> CreateAsync(CreateNotificationRequestDto dto, CancellationToken ct = default)
    {
        // Validate lengths already handled by FluentValidation when used by controllers.

        var now = Clock.UtcNowTz;
        var createdBy = _currentUser.UserName ?? _currentUser.UserId.ToString();

        var n = new Notification
        {
            RecipientId = dto.RecipientId,
            RecipientName = dto.RecipientName,
            Type = dto.Type,
            Title = dto.Title,
            Message = dto.Message,
            IsRead = false,
            ReadAt = null,
            ReferenceModule = dto.ReferenceModule,
            ReferenceId = dto.ReferenceId,
            IsActive = true,
            IsDeleted = false,
            CreatedBy = createdBy,
            CreatedDate = now,
            ModifiedBy = null,
            ModifiedDate = null
        };

        _context.Notifications.Add(n);
        await _context.SaveChangesAsync(ct);

        return new NotificationResponseDto
        {
            Id = n.Id,
            RecipientId = n.RecipientId,
            RecipientName = n.RecipientName,
            Type = n.Type,
            Title = n.Title,
            Message = n.Message,
            IsRead = n.IsRead,
            ReadAt = n.ReadAt,
            ReferenceModule = n.ReferenceModule,
            ReferenceId = n.ReferenceId,
            IsActive = n.IsActive,
            IsDeleted = n.IsDeleted,
            CreatedBy = n.CreatedBy,
            CreatedDate = n.CreatedDate
        };
    }

    public async Task<NotificationResponseDto?> GetByIdForCurrentUserAsync(long id, CancellationToken ct = default)
    {
        var rid = GetCurrentRecipientId();
        var n = await _context.Notifications.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.RecipientId == rid && x.IsActive && !x.IsDeleted, ct);
        if (n is null) return null;
        return new NotificationResponseDto
        {
            Id = n.Id,
            RecipientId = n.RecipientId,
            RecipientName = n.RecipientName,
            Type = n.Type,
            Title = n.Title,
            Message = n.Message,
            IsRead = n.IsRead,
            ReadAt = n.ReadAt,
            ReferenceModule = n.ReferenceModule,
            ReferenceId = n.ReferenceId,
            IsActive = n.IsActive,
            IsDeleted = n.IsDeleted,
            CreatedBy = n.CreatedBy,
            CreatedDate = n.CreatedDate
        };
    }

    public async Task<(List<NotificationResponseDto> Items, int Total)> GetForCurrentUserAsync(int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var rid = GetCurrentRecipientId();

        var query = _context.Notifications.AsNoTracking().Where(n => n.RecipientId == rid && n.IsActive && !n.IsDeleted);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(n => n.CreatedDate).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new NotificationResponseDto
            {
                Id = n.Id,
                RecipientId = n.RecipientId,
                RecipientName = n.RecipientName,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                IsRead = n.IsRead,
                ReadAt = n.ReadAt,
                ReferenceModule = n.ReferenceModule,
                ReferenceId = n.ReferenceId,
                IsActive = n.IsActive,
                IsDeleted = n.IsDeleted,
                CreatedBy = n.CreatedBy,
                CreatedDate = n.CreatedDate
            }).ToListAsync(ct);

        return (items, total);
    }

    public async Task<(List<NotificationResponseDto> Items, int Total)> GetUnreadForCurrentUserAsync(int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var rid = GetCurrentRecipientId();

        var query = _context.Notifications.AsNoTracking().Where(n => n.RecipientId == rid && !n.IsRead && n.IsActive && !n.IsDeleted);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(n => n.CreatedDate).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new NotificationResponseDto
            {
                Id = n.Id,
                RecipientId = n.RecipientId,
                RecipientName = n.RecipientName,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                IsRead = n.IsRead,
                ReadAt = n.ReadAt,
                ReferenceModule = n.ReferenceModule,
                ReferenceId = n.ReferenceId,
                IsActive = n.IsActive,
                IsDeleted = n.IsDeleted,
                CreatedBy = n.CreatedBy,
                CreatedDate = n.CreatedDate
            }).ToListAsync(ct);

        return (items, total);
    }

    public async Task<NotificationResponseDto?> MarkReadAsync(long id, CancellationToken ct = default)
    {
        var rid = GetCurrentRecipientId();
        var n = await _context.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.RecipientId == rid && x.IsActive && !x.IsDeleted, ct);
        if (n is null) return null;
        if (!n.IsRead)
        {
            n.IsRead = true;
            n.ReadAt = Clock.UtcNowTz;
            n.ModifiedBy = _currentUser.UserName ?? _currentUser.UserId.ToString();
            n.ModifiedDate = Clock.UtcNowTz;
            _context.Notifications.Update(n);
            await _context.SaveChangesAsync(ct);
        }

        return new NotificationResponseDto
        {
            Id = n.Id,
            RecipientId = n.RecipientId,
            RecipientName = n.RecipientName,
            Type = n.Type,
            Title = n.Title,
            Message = n.Message,
            IsRead = n.IsRead,
            ReadAt = n.ReadAt,
            ReferenceModule = n.ReferenceModule,
            ReferenceId = n.ReferenceId,
            IsActive = n.IsActive,
            IsDeleted = n.IsDeleted,
            CreatedBy = n.CreatedBy,
            CreatedDate = n.CreatedDate
        };
    }

    public async Task<int> MarkAllReadAsync(CancellationToken ct = default)
    {
        var rid = GetCurrentRecipientId();
        var now = Clock.UtcNowTz;
        var modifiedBy = _currentUser.UserName ?? _currentUser.UserId.ToString();

        var items = await _context.Notifications.Where(n => n.RecipientId == rid && !n.IsRead && n.IsActive && !n.IsDeleted).ToListAsync(ct);
        foreach (var n in items)
        {
            n.IsRead = true;
            n.ReadAt = now;
            n.ModifiedBy = modifiedBy;
            n.ModifiedDate = now;
        }

        if (items.Count > 0)
        {
            _context.Notifications.UpdateRange(items);
            await _context.SaveChangesAsync(ct);
        }

        return items.Count;
    }
}
