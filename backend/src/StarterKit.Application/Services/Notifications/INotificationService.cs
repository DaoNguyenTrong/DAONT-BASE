using StarterKit.Application.Common.Models;
using StarterKit.Domain.Entities;

namespace StarterKit.Application.Services.Notifications;

public interface INotificationService
{
    Task NotifyAsync(NotificationParams request, CancellationToken cancellationToken);

    Task<PagedResult<NotificationDto>> GetMyNotificationsAsync(
        PaginationRequest request, bool? unreadOnly, CancellationToken cancellationToken);

    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken);

    Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken);

    Task MarkAllAsReadAsync(CancellationToken cancellationToken);
}
