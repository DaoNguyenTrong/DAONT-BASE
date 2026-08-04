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
                    channel.Name, notificationId);
            }
        }
    }
}
