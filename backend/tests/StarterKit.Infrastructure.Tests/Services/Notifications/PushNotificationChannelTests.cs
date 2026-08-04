using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Models;
using StarterKit.Application.Services.Notifications;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Interfaces;
using StarterKit.Infrastructure.Services.Notifications;

namespace StarterKit.Infrastructure.Tests.Services.Notifications;

public class PushNotificationChannelTests
{
    private sealed record Fixture(
        PushNotificationChannel Channel,
        IPushSender PushSender,
        IUnitOfWork UnitOfWork,
        IRepository<PushSubscription, Guid> SubscriptionRepo);

    private static Fixture CreateFixture()
    {
        IPushSender pushSender = Substitute.For<IPushSender>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<PushSubscription, Guid> subscriptionRepo = Substitute.For<IRepository<PushSubscription, Guid>>();
        unitOfWork.Repository<PushSubscription, Guid>().Returns(subscriptionRepo);

        PushNotificationChannel channel = new(
            pushSender, unitOfWork, NullLogger<PushNotificationChannel>.Instance);

        return new Fixture(channel, pushSender, unitOfWork, subscriptionRepo);
    }

    private static PushSubscription CreateSubscription(Guid accountId, string token = "token-1") =>
        PushSubscription.Create(new PushSubscriptionParams(accountId, token, "Web"));

    [Fact]
    public async Task SendAsync_KnownTypeWithSubscriptions_SendsToPushSender()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        Notification notification = Notification.Create(
            new NotificationParams(accountId, NotificationTypes.OrganizationMemberAdded, """{"organizationName":"Acme"}"""));
        PushSubscription[] subscriptions = [CreateSubscription(accountId, "token-1"), CreateSubscription(accountId, "token-2")];
        f.SubscriptionRepo.ListAsync(
            Arg.Any<Expression<Func<PushSubscription, bool>>>(), Arg.Any<CancellationToken>()).Returns(subscriptions);
        f.PushSender.SendAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<PushMessage>(), Arg.Any<CancellationToken>())
            .Returns(new PushSendResult([], 2, 0));

        await f.Channel.SendAsync(notification, CancellationToken.None);

        await f.PushSender.Received(1).SendAsync(
            Arg.Is<IReadOnlyList<string>>(tokens => tokens != null && tokens.Count == 2 && tokens.Contains("token-1") && tokens.Contains("token-2")),
            Arg.Is<PushMessage>(m => m != null && m.Body.Contains("Acme")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_NoSubscriptions_SkipsWithoutCallingPushSender()
    {
        Fixture f = CreateFixture();
        Notification notification = Notification.Create(
            new NotificationParams(Guid.NewGuid(), NotificationTypes.OrganizationMemberAdded));
        f.SubscriptionRepo.ListAsync(
            Arg.Any<Expression<Func<PushSubscription, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PushSubscription>());

        await f.Channel.SendAsync(notification, CancellationToken.None);

        await f.PushSender.DidNotReceive().SendAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<PushMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_UnknownType_SkipsWithoutTouchingRepoOrSender()
    {
        Fixture f = CreateFixture();
        Notification notification = Notification.Create(new NotificationParams(Guid.NewGuid(), "SomeUnknownType"));

        await f.Channel.SendAsync(notification, CancellationToken.None);

        await f.SubscriptionRepo.DidNotReceive().ListAsync(
            Arg.Any<Expression<Func<PushSubscription, bool>>>(), Arg.Any<CancellationToken>());
        await f.PushSender.DidNotReceive().SendAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<PushMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_InvalidTokenInResponse_DeletesSubscription()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        Notification notification = Notification.Create(
            new NotificationParams(accountId, NotificationTypes.OrganizationMemberAdded));
        PushSubscription valid = CreateSubscription(accountId, "token-valid");
        PushSubscription invalid = CreateSubscription(accountId, "token-invalid");
        f.SubscriptionRepo.ListAsync(
            Arg.Any<Expression<Func<PushSubscription, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([valid, invalid]);
        f.PushSender.SendAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<PushMessage>(), Arg.Any<CancellationToken>())
            .Returns(new PushSendResult(["token-invalid"], 1, 1));

        await f.Channel.SendAsync(notification, CancellationToken.None);

        f.SubscriptionRepo.Received(1).Delete(invalid);
        f.SubscriptionRepo.DidNotReceive().Delete(valid);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
