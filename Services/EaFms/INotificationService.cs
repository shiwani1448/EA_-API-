using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface INotificationService
{
    Task<NotificationResponseDto> CreateAsync(CreateNotificationRequestDto dto, CancellationToken ct = default);

    Task<NotificationResponseDto?> GetByIdForCurrentUserAsync(long id, CancellationToken ct = default);

    Task<(List<NotificationResponseDto> Items, int Total)> GetForCurrentUserAsync(int page = 1, int pageSize = 50, CancellationToken ct = default);

    Task<(List<NotificationResponseDto> Items, int Total)> GetUnreadForCurrentUserAsync(int page = 1, int pageSize = 50, CancellationToken ct = default);

    Task<NotificationResponseDto?> MarkReadAsync(long id, CancellationToken ct = default);

    Task<int> MarkAllReadAsync(CancellationToken ct = default);
}
