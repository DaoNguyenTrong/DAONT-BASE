namespace StarterKit.Application.Services.Notifications;

public interface IPushSubscriptionService
{
    Task RegisterAsync(RegisterPushSubscriptionRequest request, CancellationToken cancellationToken);

    Task RemoveAsync(string token, CancellationToken cancellationToken);

    Task<PushSubscriptionStatusResponse> GetStatusAsync(CancellationToken cancellationToken);
}
