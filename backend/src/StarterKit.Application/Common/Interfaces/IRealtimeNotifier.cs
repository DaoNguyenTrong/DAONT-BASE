using StarterKit.Application.Services.Notifications;

namespace StarterKit.Application.Common.Interfaces;

public interface IRealtimeNotifier
{
    Task NotifyAsync(Guid accountId, NotificationDto notification, CancellationToken cancellationToken);
}
