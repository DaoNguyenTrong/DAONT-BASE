using Microsoft.AspNetCore.SignalR;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Services.Notifications;

namespace StarterKit.Infrastructure.Services.Notifications;

internal sealed class SignalRRealtimeNotifier(IHubContext<NotificationHub> hubContext) : IRealtimeNotifier
{
    private const string ReceiveNotificationMethod = "ReceiveNotification";

    public Task NotifyAsync(Guid accountId, NotificationDto notification, CancellationToken cancellationToken) =>
        hubContext.Clients.User(accountId.ToString())
            .SendAsync(ReceiveNotificationMethod, notification, cancellationToken);
}
