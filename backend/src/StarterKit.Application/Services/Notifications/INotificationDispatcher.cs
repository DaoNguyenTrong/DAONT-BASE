namespace StarterKit.Application.Services.Notifications;

public interface INotificationDispatcher
{
    Task DispatchAsync(Guid notificationId, CancellationToken cancellationToken);
}
