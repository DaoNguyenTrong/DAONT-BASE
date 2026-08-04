using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Mappings;
using StarterKit.Application.Common.Models;
using StarterKit.Application.Resources;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Services.Notifications;

public sealed class NotificationService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    IBackgroundJobDispatcher jobDispatcher,
    ILogger<NotificationService> logger) : INotificationService
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 10;

    public async Task NotifyAsync(NotificationParams request, CancellationToken cancellationToken)
    {
        Notification notification = Notification.Create(request);
        await unitOfWork.Repository<Notification, Guid>().AddAsync(notification, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            jobDispatcher.Enqueue<INotificationDispatcher>(
                dispatcher => dispatcher.DispatchAsync(notification.Id, CancellationToken.None),
                NotificationQueues.Default);
        }
        catch (Exception exception)
        {
            // In-app notification đã commit — lỗi enqueue không được biến 1 NotifyAsync
            // thành công thành lỗi phía caller.
            logger.LogError(exception, "Failed to enqueue dispatch for notification {NotificationId}", notification.Id);
        }
    }

    public async Task<PagedResult<NotificationDto>> GetMyNotificationsAsync(
        PaginationRequest request, bool? unreadOnly, CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();
        int pageNumber = request.PageNumber < 1 ? DefaultPageNumber : request.PageNumber;
        int pageSize = request.PageSize < 1 ? DefaultPageSize : request.PageSize;

        Expression<Func<Notification, bool>> predicate = unreadOnly == true
            ? n => n.AccountId == accountId && n.ReadAt == null
            : n => n.AccountId == accountId;

        (IReadOnlyList<Notification> items, int totalCount) = await unitOfWork
            .Repository<Notification, Guid>()
            .ListPagedAsync(predicate, pageNumber, pageSize, cancellationToken);

        return new PagedResult<NotificationDto>(
            items.Select(n => EntityMapper.ToDto(n)).ToList(), totalCount, pageNumber, pageSize);
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();

        (_, int totalCount) = await unitOfWork.Repository<Notification, Guid>()
            .ListPagedAsync(n => n.AccountId == accountId && n.ReadAt == null, 1, 1, cancellationToken);

        return totalCount;
    }

    public async Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();
        IRepository<Notification, Guid> repository = unitOfWork.Repository<Notification, Guid>();

        Notification notification = await repository.GetByIdAsync(notificationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Notification), notificationId);

        if (notification.AccountId != accountId)
        {
            throw new NotFoundException(nameof(Notification), notificationId);
        }

        notification.MarkRead();
        repository.Update(notification);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllAsReadAsync(CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();
        IRepository<Notification, Guid> repository = unitOfWork.Repository<Notification, Guid>();

        IReadOnlyList<Notification> unread = await repository.ListAsync(
            n => n.AccountId == accountId && n.ReadAt == null, cancellationToken);

        foreach (Notification notification in unread)
        {
            notification.MarkRead();
            repository.Update(notification);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private Guid GetCurrentAccountId()
    {
        if (!Guid.TryParse(currentUserService.UserId, out Guid accountId))
        {
            throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);
        }

        return accountId;
    }
}
