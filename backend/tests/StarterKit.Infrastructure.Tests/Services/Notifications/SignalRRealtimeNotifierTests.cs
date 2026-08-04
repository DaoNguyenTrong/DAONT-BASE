using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using StarterKit.Application.Services.Notifications;
using StarterKit.Infrastructure.Services.Notifications;

namespace StarterKit.Infrastructure.Tests.Services.Notifications;

public class SignalRRealtimeNotifierTests
{
    private sealed record Fixture(
        SignalRRealtimeNotifier Notifier,
        IHubClients Clients,
        IClientProxy Proxy);

    private static Fixture CreateFixture(Guid accountId)
    {
        IHubContext<NotificationHub> hubContext = Substitute.For<IHubContext<NotificationHub>>();
        IHubClients clients = Substitute.For<IHubClients>();
        IClientProxy proxy = Substitute.For<IClientProxy>();

        hubContext.Clients.Returns(clients);
        clients.User(accountId.ToString()).Returns(proxy);

        SignalRRealtimeNotifier notifier = new(hubContext);

        return new Fixture(notifier, clients, proxy);
    }

    [Fact]
    public async Task NotifyAsync_Always_SendsToCorrectUser()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        NotificationDto dto = new(Guid.NewGuid(), "OrganizationMemberAdded", null, false, DateTime.UtcNow);

        await f.Notifier.NotifyAsync(accountId, dto, CancellationToken.None);

        f.Clients.Received(1).User(accountId.ToString());
    }

    [Fact]
    public async Task NotifyAsync_Always_SendsNotificationPayloadOnReceiveNotificationMethod()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        NotificationDto dto = new(Guid.NewGuid(), "OrganizationMemberAdded", null, false, DateTime.UtcNow);

        await f.Notifier.NotifyAsync(accountId, dto, CancellationToken.None);

        await f.Proxy.Received(1).SendCoreAsync(
            "ReceiveNotification",
            Arg.Is<object?[]>(args => args != null && args.Length == 1 && Equals(args[0], dto)),
            Arg.Any<CancellationToken>());
    }
}
