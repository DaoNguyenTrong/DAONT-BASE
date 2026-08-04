using Microsoft.Extensions.Logging;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Models;
using StarterKit.Application.Services.Notifications;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Infrastructure.Services.Notifications;

internal sealed class PushNotificationChannel(
    IPushSender pushSender,
    IUnitOfWork unitOfWork,
    ILogger<PushNotificationChannel> logger) : INotificationChannel
{
    public string Name => "Push";

    public async Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        (string Title, string Body)? content =
            NotificationPushTemplates.TryRender(notification.Type, notification.Data);

        if (content is null)
        {
            logger.LogDebug(
                "No push template registered for notification type {Type} — skipping push channel.",
                notification.Type);
            return;
        }

        IRepository<PushSubscription, Guid> repository = unitOfWork.Repository<PushSubscription, Guid>();
        IReadOnlyList<PushSubscription> subscriptions = await repository.ListAsync(
            s => s.AccountId == notification.AccountId, cancellationToken);

        if (subscriptions.Count == 0)
        {
            logger.LogDebug(
                "No active push subscriptions for account {AccountId} — skipping push for notification {NotificationId}",
                notification.AccountId, notification.Id);
            return;
        }

        List<string> tokens = subscriptions.Select(s => s.Token).ToList();
        PushSendResult result = await pushSender.SendAsync(
            tokens, new PushMessage(content.Value.Title, content.Value.Body), cancellationToken);

        if (result.InvalidTokens.Count == 0)
        {
            return;
        }

        foreach (PushSubscription subscription in subscriptions.Where(s => result.InvalidTokens.Contains(s.Token)))
        {
            repository.Delete(subscription);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
