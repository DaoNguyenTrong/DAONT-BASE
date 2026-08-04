using StarterKit.Domain.Entities;

namespace StarterKit.Application.Common.Interfaces;

public interface INotificationChannel
{
    string Name { get; }

    Task SendAsync(Notification notification, CancellationToken cancellationToken);
}
