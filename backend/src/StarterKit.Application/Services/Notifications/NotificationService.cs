using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using StarterKit.Application.Common.Interfaces;
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
    IRealtimeNotifier realtimeNotifier,
    ILogger<NotificationService> logger) : INotificationService
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 10;

    // Hangfire stores job arguments in its database; a fan-out of thousands of ids in one job would sit in
    // the queue as a JSON blob tens of KB long.
    private const int DispatchChunkSize = 500;

    // Each push is a backplane round trip: one at a time, a 5000-recipient fan-out waited ~0.6s even on a local
    // Redis (~50ms concurrently). Bounded, so a huge fan-out doesn't start every send at once.
    private const int RealtimePushConcurrency = 32;

    public Task NotifyAsync(NotificationParams request, CancellationToken cancellationToken) =>
        NotifyManyAsync(
            [new NotificationBroadcast([request.AccountId], request.Type, request.Data, request.DedupeKey)],
            cancellationToken);

    public async Task<int> NotifyManyAsync(
        IReadOnlyCollection<NotificationBroadcast> broadcasts, CancellationToken cancellationToken)
    {
        // Built up front, so a broadcast that fails domain validation throws before anything is saved.
        List<Notification> candidates = [.. broadcasts.SelectMany(broadcast => broadcast.AccountIds
            .Distinct()
            .Select(accountId => Notification.Create(
                new NotificationParams(accountId, broadcast.Type, broadcast.Data, broadcast.DedupeKey))))];

        if (candidates.Count == 0)
        {
            return 0;
        }

        IRepository<Notification, Guid> repository = unitOfWork.Repository<Notification, Guid>();
        HashSet<(Guid, string)> sent = await LoadSentKeysAsync(repository, candidates, cancellationToken);

        List<Notification> created = [];
        foreach (Notification notification in candidates)
        {
            // Also catches a key repeated inside this batch: letting it through would fail the unique index at
            // SaveChanges and take the whole batch down with it.
            if (notification.DedupeKey is { } key && !sent.Add((notification.AccountId, key)))
            {
                continue;
            }

            await repository.AddAsync(notification, cancellationToken);
            created.Add(notification);
        }

        if (created.Count == 0)
        {
            return 0;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await PushRealtimeAsync(created, cancellationToken);
        EnqueueDispatch(created);

        return created.Count;
    }

    public async Task<PagedResult<NotificationDto>> GetMyNotificationsAsync(
        NotificationListQuery query, CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();
        int pageNumber = query.PageNumber < 1 ? DefaultPageNumber : query.PageNumber;
        int pageSize = query.PageSize < 1 ? DefaultPageSize : query.PageSize;

        Expression<Func<Notification, bool>> predicate = query.ReadState switch
        {
            NotificationReadState.Unread => n => n.AccountId == accountId && n.ReadAt == null,
            NotificationReadState.Read => n => n.AccountId == accountId && n.ReadAt != null,
            _ => n => n.AccountId == accountId
        };

        (IReadOnlyList<Notification> items, int totalCount) = await unitOfWork
            .Repository<Notification, Guid>()
            .ListPagedAsync(predicate, pageNumber, pageSize, cancellationToken);

        return new PagedResult<NotificationDto>(
            items.Select(NotificationMapper.ToDto).ToList(), totalCount, pageNumber, pageSize);
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

    // The (recipient, key) pairs already stored — one read for the batch, only those two columns.
    private static async Task<HashSet<(Guid, string)>> LoadSentKeysAsync(
        IRepository<Notification, Guid> repository,
        IReadOnlyList<Notification> candidates,
        CancellationToken cancellationToken)
    {
        List<string> keys = [.. candidates.Select(n => n.DedupeKey).OfType<string>().Distinct()];
        if (keys.Count == 0)
        {
            return [];
        }

        List<Guid> accountIds = [.. candidates.Where(n => n.DedupeKey is not null).Select(n => n.AccountId).Distinct()];
        // The constructor, not a (a, b) literal: expression trees can't contain tuple literals. A public type keeps
        // repository stubs in tests working without an InternalsVisibleTo grant to the mocking library.
        IReadOnlyList<ValueTuple<Guid, string>> existing = await repository.ListAsync(
            n => n.DedupeKey != null && keys.Contains(n.DedupeKey) && accountIds.Contains(n.AccountId),
            // The predicate already excludes null; ?? keeps the projection free of the ! operator.
            n => new ValueTuple<Guid, string>(n.AccountId, n.DedupeKey ?? string.Empty),
            cancellationToken);

        return [.. existing];
    }

    // The in-app rows are committed by now: neither a failed realtime push nor a failed enqueue may turn a
    // successful notify into an error for the caller. Each recipient/chunk is tried on its own, so one failure
    // doesn't cost the rest theirs; the batch logs once, since a broken hub or storage fails them all alike.
    private async Task PushRealtimeAsync(IReadOnlyList<Notification> created, CancellationToken cancellationToken)
    {
        int failed = 0;
        Exception? firstFailure = null;
        ParallelOptions options = new()
        {
            MaxDegreeOfParallelism = RealtimePushConcurrency,
            CancellationToken = cancellationToken
        };

        try
        {
            await Parallel.ForEachAsync(created, options, async (notification, token) =>
            {
                try
                {
                    await realtimeNotifier.NotifyAsync(
                        notification.AccountId, NotificationMapper.ToDto(notification), token);
                }
                catch (Exception exception)
                {
                    Interlocked.Increment(ref failed);
                    Interlocked.CompareExchange(ref firstFailure, exception, null);
                }
            });
        }
        catch (OperationCanceledException exception)
        {
            // The request was aborted mid-fan-out: the pushes not yet sent are lost, the committed rows aren't.
            firstFailure ??= exception;
        }

        if (firstFailure is not null)
        {
            // Clients still see these through the poll fallback.
            logger.LogError(firstFailure, "Failed to push {Failed} of {Count} realtime notifications",
                failed, created.Count);
        }
    }

    private void EnqueueDispatch(IReadOnlyList<Notification> created)
    {
        int failed = 0;
        Exception? firstFailure = null;
        foreach (Notification[] chunk in created.Chunk(DispatchChunkSize))
        {
            List<Guid> ids = [.. chunk.Select(notification => notification.Id)];
            try
            {
                jobDispatcher.Enqueue<INotificationDispatcher>(
                    dispatcher => dispatcher.DispatchManyAsync(ids, CancellationToken.None),
                    NotificationQueues.Default);
            }
            catch (Exception exception)
            {
                failed += ids.Count;
                firstFailure ??= exception;
            }
        }

        if (firstFailure is not null)
        {
            // Not retried: a caller's retry would dedupe these rows away, so they get no email/push.
            logger.LogError(firstFailure, "Failed to enqueue dispatch for {Failed} of {Count} notifications",
                failed, created.Count);
        }
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
