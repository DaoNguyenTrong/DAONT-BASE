using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Models;
using StarterKit.Application.Resources;
using StarterKit.Application.Services.Notifications;
using StarterKit.Application.Tests.TestSupport;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Tests.Services.Notifications;

public class NotificationServiceTests
{
    private sealed record Fixture(
        NotificationService Service,
        IRepository<Notification, Guid> NotificationRepo,
        IUnitOfWork UnitOfWork,
        ICurrentUserService CurrentUserService,
        IBackgroundJobDispatcher JobDispatcher,
        IRealtimeNotifier RealtimeNotifier);

    private static Fixture CreateFixture()
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<Notification, Guid> notificationRepo = Substitute.For<IRepository<Notification, Guid>>();
        unitOfWork.Repository<Notification, Guid>().Returns(notificationRepo);

        ICurrentUserService currentUserService = Substitute.For<ICurrentUserService>();
        IBackgroundJobDispatcher jobDispatcher = Substitute.For<IBackgroundJobDispatcher>();
        IRealtimeNotifier realtimeNotifier = Substitute.For<IRealtimeNotifier>();

        NotificationService service = new(
            unitOfWork, currentUserService, jobDispatcher, realtimeNotifier, NullLogger<NotificationService>.Instance);

        return new Fixture(service, notificationRepo, unitOfWork, currentUserService, jobDispatcher, realtimeNotifier);
    }

    private static Notification CreateNotification(Guid accountId, string type = "OrganizationMemberAdded") =>
        Notification.Create(new NotificationParams(accountId, type));

    // Stands in for the database: the service's own predicate and projection run over these rows.
    private static void StoreExisting(Fixture f, params Notification[] stored) =>
        f.NotificationRepo
            .ListAsync(
                Arg.Any<Expression<Func<Notification, bool>>>(),
                Arg.Any<Expression<Func<Notification, ValueTuple<Guid, string>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => (IReadOnlyList<ValueTuple<Guid, string>>)
            [
                .. stored
                    .Where(call.ArgAt<Expression<Func<Notification, bool>>>(0).Compile())
                    .Select(call.ArgAt<Expression<Func<Notification, ValueTuple<Guid, string>>>>(1).Compile())
            ]);

    // NotifyAsync

    [Fact]
    public async Task NotifyAsync_Always_PersistsNotification()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();

        await f.Service.NotifyAsync(
            new NotificationParams(accountId, NotificationTypes.OrganizationMemberAdded), CancellationToken.None);

        await f.NotificationRepo.Received(1).AddAsync(
            Arg.Is<Notification>(n => n != null && n.AccountId == accountId), Arg.Any<CancellationToken>());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAsync_Success_EnqueuesDispatchOnNotificationQueue()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();

        await f.Service.NotifyAsync(
            new NotificationParams(accountId, NotificationTypes.OrganizationMemberAdded), CancellationToken.None);

        f.JobDispatcher.Received(1).Enqueue(
            Arg.Any<Expression<Func<INotificationDispatcher, Task>>>(), NotificationQueues.Default);
    }

    [Fact]
    public async Task NotifyAsync_EnqueueThrows_DoesNotPropagate()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.JobDispatcher
            .When(d => d.Enqueue(
                Arg.Any<Expression<Func<INotificationDispatcher, Task>>>(), Arg.Any<string>()))
            .Do(_ => throw new InvalidOperationException("hangfire down"));

        await f.Service.NotifyAsync(
            new NotificationParams(accountId, NotificationTypes.OrganizationMemberAdded), CancellationToken.None);

        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAsync_Success_PushesRealtimeNotification()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();

        await f.Service.NotifyAsync(
            new NotificationParams(accountId, NotificationTypes.OrganizationMemberAdded), CancellationToken.None);

        await f.RealtimeNotifier.Received(1).NotifyAsync(
            accountId, Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAsync_RealtimePushThrows_DoesNotPropagate()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.RealtimeNotifier
            .NotifyAsync(Arg.Any<Guid>(), Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("hub unavailable"));

        await f.Service.NotifyAsync(
            new NotificationParams(accountId, NotificationTypes.OrganizationMemberAdded), CancellationToken.None);

        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAsync_RealtimePushThrows_StillEnqueuesDispatch()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.RealtimeNotifier
            .NotifyAsync(Arg.Any<Guid>(), Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("hub unavailable"));

        await f.Service.NotifyAsync(
            new NotificationParams(accountId, NotificationTypes.OrganizationMemberAdded), CancellationToken.None);

        f.JobDispatcher.Received(1).Enqueue(
            Arg.Any<Expression<Func<INotificationDispatcher, Task>>>(), NotificationQueues.Default);
    }

    // NotifyManyAsync

    [Fact]
    public async Task NotifyManyAsync_SavesOnceAndEnqueuesOneJobForTheWholeFanOut()
    {
        Fixture f = CreateFixture();
        Guid[] accounts = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

        int created = await f.Service.NotifyManyAsync(
            [new NotificationBroadcast(accounts, NotificationTypes.OrganizationMemberAdded),
             new NotificationBroadcast([accounts[0]], NotificationTypes.OrganizationMemberAdded)],
            CancellationToken.None);

        Assert.Equal(4, created);
        await f.NotificationRepo.Received(4).AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        f.JobDispatcher.Received(1).Enqueue(
            Arg.Any<Expression<Func<INotificationDispatcher, Task>>>(), NotificationQueues.Default);
        await f.RealtimeNotifier.Received(4).NotifyAsync(
            Arg.Any<Guid>(), Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>());
    }

    // Hangfire stores job arguments in its database: one job per 500 ids keeps each payload small.
    [Fact]
    public async Task NotifyManyAsync_LargeFanOut_SplitsDispatchIntoChunks()
    {
        Fixture f = CreateFixture();
        Guid[] accounts = [.. Enumerable.Range(0, 1001).Select(_ => Guid.NewGuid())];

        await f.Service.NotifyManyAsync(
            [new NotificationBroadcast(accounts, NotificationTypes.OrganizationMemberAdded)], CancellationToken.None);

        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        f.JobDispatcher.Received(3).Enqueue(
            Arg.Any<Expression<Func<INotificationDispatcher, Task>>>(), NotificationQueues.Default);
    }

    // One recipient's push failing (a transient backplane error) must not cost everyone after it theirs.
    [Fact]
    public async Task NotifyManyAsync_OneRealtimePushFails_StillPushesTheRest()
    {
        Fixture f = CreateFixture();
        Guid[] accounts = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        f.RealtimeNotifier
            .NotifyAsync(accounts[0], Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("backplane hiccup"));

        await f.Service.NotifyManyAsync(
            [new NotificationBroadcast(accounts, NotificationTypes.OrganizationMemberAdded)], CancellationToken.None);

        await f.RealtimeNotifier.Received(1).NotifyAsync(accounts[1], Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>());
        await f.RealtimeNotifier.Received(1).NotifyAsync(accounts[2], Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>());
    }

    // One awaited send per recipient, one after another, made a large fan-out's request wait out every backplane
    // round trip (measured over Redis: ~0.6s sequential vs ~50ms concurrent for 5000 recipients).
    [Fact]
    public async Task NotifyManyAsync_PushesToRecipientsConcurrently()
    {
        Fixture f = CreateFixture();
        Guid[] accounts = [.. Enumerable.Range(0, 8).Select(_ => Guid.NewGuid())];
        int inFlight = 0;
        int maxInFlight = 0;
        f.RealtimeNotifier
            .NotifyAsync(Arg.Any<Guid>(), Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                int now = Interlocked.Increment(ref inFlight);
                InterlockedMax(ref maxInFlight, now);
                await Task.Delay(50);
                Interlocked.Decrement(ref inFlight);
            });

        await f.Service.NotifyManyAsync(
            [new NotificationBroadcast(accounts, NotificationTypes.OrganizationMemberAdded)], CancellationToken.None);

        Assert.True(maxInFlight > 1, $"pushes ran one at a time (max in flight: {maxInFlight})");
        await f.RealtimeNotifier.Received(8).NotifyAsync(
            Arg.Any<Guid>(), Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>());
    }

    // The rows are committed: a request aborted mid-fan-out loses the remaining pushes (clients poll), not
    // the notify call itself.
    [Fact]
    public async Task NotifyManyAsync_AbortedDuringThePush_DoesNotThrow()
    {
        Fixture f = CreateFixture();
        using CancellationTokenSource aborted = new();
        f.RealtimeNotifier
            .NotifyAsync(Arg.Any<Guid>(), Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await aborted.CancelAsync();
                call.Arg<CancellationToken>().ThrowIfCancellationRequested();
            });

        int created = await f.Service.NotifyManyAsync(
            [new NotificationBroadcast([Guid.NewGuid(), Guid.NewGuid()], NotificationTypes.OrganizationMemberAdded)],
            aborted.Token);

        Assert.Equal(2, created);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)) &&
               Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }

    // The rows are committed and a retry would dedupe them away, so a failed chunk must not take later ones down.
    [Fact]
    public async Task NotifyManyAsync_OneDispatchChunkFails_StillEnqueuesTheRest()
    {
        Fixture f = CreateFixture();
        Guid[] accounts = [.. Enumerable.Range(0, 1001).Select(_ => Guid.NewGuid())];
        int calls = 0;
        f.JobDispatcher
            .When(d => d.Enqueue(Arg.Any<Expression<Func<INotificationDispatcher, Task>>>(), Arg.Any<string>()))
            .Do(_ =>
            {
                if (++calls == 1)
                {
                    throw new InvalidOperationException("hangfire storage hiccup");
                }
            });

        await f.Service.NotifyManyAsync(
            [new NotificationBroadcast(accounts, NotificationTypes.OrganizationMemberAdded)], CancellationToken.None);

        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task NotifyManyAsync_SkipsRecipientsThatAlreadyHaveTheKey_AndDuplicatesInsideTheBatch()
    {
        Fixture f = CreateFixture();
        Guid alreadyNotified = Guid.NewGuid();
        Guid fresh = Guid.NewGuid();
        StoreExisting(f, Notification.Create(new NotificationParams(alreadyNotified, "Type", DedupeKey: "event-1")));

        int created = await f.Service.NotifyManyAsync(
            [new NotificationBroadcast([alreadyNotified, fresh, fresh], "Type", DedupeKey: "event-1"),
             new NotificationBroadcast([fresh], "Type", DedupeKey: "event-1")],
            CancellationToken.None);

        Assert.Equal(1, created);
        await f.NotificationRepo.Received(1).AddAsync(
            Arg.Is<Notification>(n => n != null && n.AccountId == fresh && n.DedupeKey == "event-1"), Arg.Any<CancellationToken>());
        // Only the (recipient, key) pairs are read — not whole rows with their Data payload.
        await f.NotificationRepo.DidNotReceive().ListAsync(
            Arg.Any<Expression<Func<Notification, bool>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyManyAsync_EverythingAlreadySent_DoesNotSaveOrEnqueue()
    {
        Fixture f = CreateFixture();
        Guid account = Guid.NewGuid();
        StoreExisting(f, Notification.Create(new NotificationParams(account, "Type", DedupeKey: "event-1")));

        int created = await f.Service.NotifyManyAsync(
            [new NotificationBroadcast([account], "Type", DedupeKey: "event-1")], CancellationToken.None);

        Assert.Equal(0, created);
        await f.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        f.JobDispatcher.DidNotReceiveWithAnyArgs().Enqueue<INotificationDispatcher>(default!, default!);
    }

    // Single-recipient sends go through the same dedupe instead of failing on the unique index.
    [Fact]
    public async Task NotifyAsync_KeyAlreadySent_IsSkippedSilently()
    {
        Fixture f = CreateFixture();
        Guid account = Guid.NewGuid();
        StoreExisting(f, Notification.Create(new NotificationParams(account, "Type", DedupeKey: "event-1")));

        await f.Service.NotifyAsync(new NotificationParams(account, "Type", DedupeKey: "event-1"), CancellationToken.None);

        await f.NotificationRepo.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await f.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // GetMyNotificationsAsync

    [Fact]
    public async Task GetMyNotificationsAsync_NotAuthenticated_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        f.CurrentUserService.UserId.Returns((string?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.AuthenticatedUserRequired,
            () => f.Service.GetMyNotificationsAsync(new NotificationListQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task GetMyNotificationsAsync_MapsItemsAndPaging()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());

        Notification notification = CreateNotification(accountId);
        f.NotificationRepo.ListPagedAsync(
                Arg.Any<Expression<Func<Notification, bool>>>(), 2, 5, Arg.Any<CancellationToken>())
            .Returns(([notification], 11));

        PagedResult<NotificationDto> result = await f.Service.GetMyNotificationsAsync(
            new NotificationListQuery(2, 5), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(notification.Id, result.Items[0].Id);
        Assert.Equal(notification.Type, result.Items[0].Type);
        Assert.Equal(11, result.TotalCount);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(5, result.PageSize);
    }

    [Fact]
    public async Task GetMyNotificationsAsync_InvalidPageValues_FallsBackToDefaults()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());

        f.NotificationRepo.ListPagedAsync(
                Arg.Any<Expression<Func<Notification, bool>>>(), 1, 10, Arg.Any<CancellationToken>())
            .Returns(([], 0));

        await f.Service.GetMyNotificationsAsync(new NotificationListQuery(0, 0), CancellationToken.None);

        await f.NotificationRepo.Received(1).ListPagedAsync(
            Arg.Any<Expression<Func<Notification, bool>>>(), 1, 10, Arg.Any<CancellationToken>());
    }

    // A nullable bool could only say "all" or "unread": null and false both meant all, so "read" had no way in.
    [Theory]
    [InlineData(NotificationReadState.All, true, true)]
    [InlineData(NotificationReadState.Unread, true, false)]
    [InlineData(NotificationReadState.Read, false, true)]
    public async Task GetMyNotificationsAsync_FiltersByReadState(
        NotificationReadState readState, bool includesUnread, bool includesRead)
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());
        Expression<Func<Notification, bool>>? predicate = null;
        f.NotificationRepo.ListPagedAsync(
                Arg.Do<Expression<Func<Notification, bool>>>(p => predicate = p), 1, 10, Arg.Any<CancellationToken>())
            .Returns(([], 0));

        await f.Service.GetMyNotificationsAsync(new NotificationListQuery(ReadState: readState), CancellationToken.None);

        Notification unread = CreateNotification(accountId);
        Notification read = CreateNotification(accountId);
        read.MarkRead();
        Notification someoneElses = CreateNotification(Guid.NewGuid());
        Func<Notification, bool> matches = predicate!.Compile();
        Assert.Equal(includesUnread, matches(unread));
        Assert.Equal(includesRead, matches(read));
        Assert.False(matches(someoneElses));
    }

    // GetUnreadCountAsync

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsTotalCountFromRepo()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());

        f.NotificationRepo.ListPagedAsync(
                Arg.Any<Expression<Func<Notification, bool>>>(), 1, 1, Arg.Any<CancellationToken>())
            .Returns(([], 3));

        int count = await f.Service.GetUnreadCountAsync(CancellationToken.None);

        Assert.Equal(3, count);
    }

    // MarkAsReadAsync

    [Fact]
    public async Task MarkAsReadAsync_NotFound_ThrowsNotFound()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());
        Guid notificationId = Guid.NewGuid();
        f.NotificationRepo.GetByIdAsync(notificationId, Arg.Any<CancellationToken>()).Returns((Notification?)null);

        await ApplicationAssert.AssertNotFoundAsync<Notification>(
            notificationId,
            () => f.Service.MarkAsReadAsync(notificationId, CancellationToken.None));
    }

    [Fact]
    public async Task MarkAsReadAsync_OwnedByOtherAccount_ThrowsNotFound()
    {
        Fixture f = CreateFixture();
        f.CurrentUserService.UserId.Returns(Guid.NewGuid().ToString());
        Notification notification = CreateNotification(Guid.NewGuid());
        f.NotificationRepo.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await ApplicationAssert.AssertNotFoundAsync<Notification>(
            notification.Id,
            () => f.Service.MarkAsReadAsync(notification.Id, CancellationToken.None));
    }

    [Fact]
    public async Task MarkAsReadAsync_OwnedByCurrentAccount_MarksReadAndSaves()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());
        Notification notification = CreateNotification(accountId);
        f.NotificationRepo.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await f.Service.MarkAsReadAsync(notification.Id, CancellationToken.None);

        Assert.True(notification.IsRead);
        f.NotificationRepo.Received(1).Update(notification);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // MarkAllAsReadAsync

    [Fact]
    public async Task MarkAllAsReadAsync_MarksEveryUnreadNotificationForAccount()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());

        Notification first = CreateNotification(accountId);
        Notification second = CreateNotification(accountId);
        f.NotificationRepo.ListAsync(Arg.Any<Expression<Func<Notification, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([first, second]);

        await f.Service.MarkAllAsReadAsync(CancellationToken.None);

        Assert.True(first.IsRead);
        Assert.True(second.IsRead);
        f.NotificationRepo.Received(1).Update(first);
        f.NotificationRepo.Received(1).Update(second);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
