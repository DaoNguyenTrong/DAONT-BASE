using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;
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
}
