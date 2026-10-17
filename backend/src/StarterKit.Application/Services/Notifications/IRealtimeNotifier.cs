namespace StarterKit.Application.Services.Notifications;

public interface IRealtimeNotifier
{
    Task NotifyAsync(Guid accountId, NotificationDto notification, CancellationToken cancellationToken);
}
