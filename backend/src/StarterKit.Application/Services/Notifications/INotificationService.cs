using StarterKit.Application.Common.Models;
using StarterKit.Domain.Entities;

namespace StarterKit.Application.Services.Notifications;

public interface INotificationService
{
    /// <summary>Single-recipient form of <see cref="NotifyManyAsync"/> — same dedupe, same write path.</summary>
    Task NotifyAsync(NotificationParams request, CancellationToken cancellationToken);

    /// <summary>
    /// Stores every broadcast for every recipient in <b>one</b> save and dispatches it in as few background jobs
    /// as the chunk size allows. A recipient that already has a broadcast's <see cref="NotificationBroadcast.DedupeKey"/>
    /// is skipped, so a re-run source doesn't notify twice; without a key nothing is deduplicated.
    /// </summary>
    /// <remarks>
    /// The skip is a check before the write. Two runs racing on the same key can both pass it; the unique index
    /// then fails the later save, and its caller (e.g. Hangfire's retry) runs again into the check.
    /// </remarks>
    /// <returns>How many notifications were actually created.</returns>
    Task<int> NotifyManyAsync(IReadOnlyCollection<NotificationBroadcast> broadcasts, CancellationToken cancellationToken);

    Task<PagedResult<NotificationDto>> GetMyNotificationsAsync(
        NotificationListQuery query, CancellationToken cancellationToken);

    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken);

    Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken);

    Task MarkAllAsReadAsync(CancellationToken cancellationToken);
}
