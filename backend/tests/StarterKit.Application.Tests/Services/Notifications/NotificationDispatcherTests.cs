using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Services.Notifications;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Tests.Services.Notifications;

public class NotificationDispatcherTests
{
    private sealed record Fixture(
        NotificationDispatcher Dispatcher,
        IRepository<Notification, Guid> NotificationRepo,
        List<INotificationChannel> Channels);

    private static Fixture CreateFixture(params INotificationChannel[] channels)
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<Notification, Guid> notificationRepo = Substitute.For<IRepository<Notification, Guid>>();
        unitOfWork.Repository<Notification, Guid>().Returns(notificationRepo);

        List<INotificationChannel> channelList = channels.ToList();

        NotificationDispatcher dispatcher = new(
            unitOfWork, channelList, NullLogger<NotificationDispatcher>.Instance);

        return new Fixture(dispatcher, notificationRepo, channelList);
    }

    private static Notification CreateNotification() =>
        Notification.Create(new NotificationParams(Guid.NewGuid(), NotificationTypes.OrganizationMemberAdded));

    [Fact]
    public async Task DispatchAsync_NotificationNotFound_NoOps()
    {
        INotificationChannel channel = Substitute.For<INotificationChannel>();
        Fixture f = CreateFixture(channel);
        Guid notificationId = Guid.NewGuid();
        f.NotificationRepo.GetByIdAsync(notificationId, Arg.Any<CancellationToken>()).Returns((Notification?)null);

        await f.Dispatcher.DispatchAsync(notificationId, CancellationToken.None);

        await channel.DidNotReceive().SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_AllChannelsSucceed_CallsEachOnce()
    {
        INotificationChannel first = Substitute.For<INotificationChannel>();
        INotificationChannel second = Substitute.For<INotificationChannel>();
        Fixture f = CreateFixture(first, second);
        Notification notification = CreateNotification();
        f.NotificationRepo.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await f.Dispatcher.DispatchAsync(notification.Id, CancellationToken.None);

        await first.Received(1).SendAsync(notification, Arg.Any<CancellationToken>());
        await second.Received(1).SendAsync(notification, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_OneChannelThrows_OtherChannelStillRuns()
    {
        INotificationChannel failing = Substitute.For<INotificationChannel>();
        failing.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("smtp down"));
        INotificationChannel healthy = Substitute.For<INotificationChannel>();
        Fixture f = CreateFixture(failing, healthy);
        Notification notification = CreateNotification();
        f.NotificationRepo.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await f.Dispatcher.DispatchAsync(notification.Id, CancellationToken.None);

        await healthy.Received(1).SendAsync(notification, Arg.Any<CancellationToken>());
    }
}
