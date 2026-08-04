using System.Linq.Expressions;
using NSubstitute;
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
        ICurrentUserService CurrentUserService);

    private static Fixture CreateFixture()
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<Notification, Guid> notificationRepo = Substitute.For<IRepository<Notification, Guid>>();
        unitOfWork.Repository<Notification, Guid>().Returns(notificationRepo);

        ICurrentUserService currentUserService = Substitute.For<ICurrentUserService>();

        NotificationService service = new(unitOfWork, currentUserService);

        return new Fixture(service, notificationRepo, unitOfWork, currentUserService);
    }

    private static Notification CreateNotification(Guid accountId, string type = "OrganizationMemberAdded") =>
        Notification.Create(new NotificationParams(accountId, type));

    // NotifyAsync

    [Fact]
    public async Task NotifyAsync_Always_PersistsNotification()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();

        await f.Service.NotifyAsync(
            new NotificationParams(accountId, NotificationTypes.OrganizationMemberAdded), CancellationToken.None);

        await f.NotificationRepo.Received(1).AddAsync(
            Arg.Is<Notification>(n => n.AccountId == accountId), Arg.Any<CancellationToken>());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // GetMyNotificationsAsync

    [Fact]
    public async Task GetMyNotificationsAsync_NotAuthenticated_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        f.CurrentUserService.UserId.Returns((string?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.AuthenticatedUserRequired,
            () => f.Service.GetMyNotificationsAsync(new PaginationRequest(), null, CancellationToken.None));
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
            new PaginationRequest(2, 5), null, CancellationToken.None);

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

        await f.Service.GetMyNotificationsAsync(new PaginationRequest(0, 0), null, CancellationToken.None);

        await f.NotificationRepo.Received(1).ListPagedAsync(
            Arg.Any<Expression<Func<Notification, bool>>>(), 1, 10, Arg.Any<CancellationToken>());
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
