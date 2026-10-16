using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Services.Notifications;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;
using StarterKit.Infrastructure.Persistence.Repositories;
using StarterKit.Infrastructure.Tests.TestSupport;

namespace StarterKit.Infrastructure.Tests.Persistence;

[Collection(nameof(PostgresCollectionB))]
public sealed class NotificationPersistenceTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private AppDbContext context = null!;
    private IDbContextTransaction transaction = null!;
    private Account account = null!;

    public async Task InitializeAsync()
    {
        context = fixture.CreateDbContext();
        transaction = await context.Database.BeginTransactionAsync();
        string name = $"notify-{Guid.NewGuid():N}";
        account = Account.Create(new AccountParams("Notify", name, $"{name}@example.com"));
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }

    // The service's pre-check can lose a race with a concurrent run; the index is what actually holds.
    [Fact]
    public async Task SameDedupeKeyForTheSameAccount_IsRejectedByTheDatabase()
    {
        context.Notifications.Add(Notification.Create(new NotificationParams(account.Id, "Type", DedupeKey: "event-1")));
        await context.SaveChangesAsync();

        context.Notifications.Add(Notification.Create(new NotificationParams(account.Id, "Type", DedupeKey: "event-1")));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task NotificationsWithoutAKey_NeverConflict()
    {
        context.Notifications.Add(Notification.Create(new NotificationParams(account.Id, "Type")));
        context.Notifications.Add(Notification.Create(new NotificationParams(account.Id, "Type")));

        await context.SaveChangesAsync();

        Assert.Equal(2, await context.Notifications.CountAsync(n => n.AccountId == account.Id));
    }

    // Through the real service and Postgres: the dedupe read is a projection EF must translate, which the
    // service's unit tests (a stubbed repository) can't show.
    [Fact]
    public async Task NotifyingTheSameKeyTwice_CreatesItOnce()
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Repository<Notification, Guid>().Returns(new GenericRepository<Notification, Guid>(context));
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(call => context.SaveChangesAsync(call.Arg<CancellationToken>()));
        NotificationService service = new(
            unitOfWork,
            Substitute.For<ICurrentUserService>(),
            Substitute.For<IBackgroundJobDispatcher>(),
            Substitute.For<IRealtimeNotifier>(),
            NullLogger<NotificationService>.Instance);
        NotificationBroadcast broadcast = new([account.Id], "Type", DedupeKey: "event-1");

        Assert.Equal(1, await service.NotifyManyAsync([broadcast], CancellationToken.None));
        Assert.Equal(0, await service.NotifyManyAsync([broadcast], CancellationToken.None));

        Assert.Equal(1, await context.Notifications.CountAsync(n => n.AccountId == account.Id));
    }
}
