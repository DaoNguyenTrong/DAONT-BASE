namespace StarterKit.Application.Services.Notifications;

public interface INotificationDispatcher
{
    /// <summary>Kept so jobs enqueued before <see cref="DispatchManyAsync"/> existed still run.</summary>
    Task DispatchAsync(Guid notificationId, CancellationToken cancellationToken);

    /// <summary>Sends a batch through the non-in-app channels — one background job per fan-out chunk.</summary>
    Task DispatchManyAsync(IReadOnlyCollection<Guid> notificationIds, CancellationToken cancellationToken);
}
