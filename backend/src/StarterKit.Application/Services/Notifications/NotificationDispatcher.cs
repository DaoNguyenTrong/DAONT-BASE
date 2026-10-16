using Microsoft.Extensions.Logging;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Services.Notifications;

public sealed class NotificationDispatcher(
    IUnitOfWork unitOfWork,
    IEnumerable<INotificationChannel> channels,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    public async Task DispatchAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        Notification? notification = await unitOfWork.Repository<Notification, Guid>()
            .GetByIdAsync(notificationId, cancellationToken);

        if (notification is null)
        {
            logger.LogWarning("Notification {NotificationId} not found — skipping dispatch.", notificationId);
            return;
        }

        await SendAsync(notification, cancellationToken);
    }

    public async Task DispatchManyAsync(IReadOnlyCollection<Guid> notificationIds, CancellationToken cancellationToken)
    {
        if (notificationIds.Count == 0)
        {
            return;
        }

        List<Guid> ids = [.. notificationIds.Distinct()];

        // One read for the batch: a type no channel has a template for is skipped by every channel anyway,
        // and loading row by row would still cost N round trips for nothing.
        IReadOnlyList<Notification> notifications = await unitOfWork.Repository<Notification, Guid>()
            .ListAsync(notification => ids.Contains(notification.Id), cancellationToken);

        if (notifications.Count < ids.Count)
        {
            // Deleted, or rolled back after the job was enqueued.
            HashSet<Guid> found = [.. notifications.Select(notification => notification.Id)];
            logger.LogWarning("{Missing} of {Count} notifications not found — skipping their dispatch: {NotificationIds}",
                ids.Count - found.Count, ids.Count, ids.Where(id => !found.Contains(id)));
        }

        foreach (Notification notification in notifications)
        {
            await SendAsync(notification, cancellationToken);
        }
    }

    private async Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        foreach (INotificationChannel channel in channels)
        {
            try
            {
                await channel.SendAsync(notification, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Notification channel {Channel} failed for notification {NotificationId}",
                    channel.Name, notification.Id);
            }
        }
    }
}
