using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;
using StarterKit.Infrastructure.Tests.TestSupport;

namespace StarterKit.Infrastructure.Tests.Persistence;

[Collection(nameof(PostgresCollectionB))]
public sealed class AccountPersistenceTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private AppDbContext context = null!;
    private IDbContextTransaction transaction = null!;

    public async Task InitializeAsync()
    {
        context = fixture.CreateDbContext();
        transaction = await context.Database.BeginTransactionAsync();
    }

    // Never committed — disposing the transaction issues a ROLLBACK, keeping tests isolated.
    public async Task DisposeAsync()
    {
        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }

    // Status has a database default of true: EF must still send an explicit false on insert instead of
    // treating it as "unset" and letting the default turn the account active.
    [Fact]
    public async Task Insert_DeactivatedAccount_StaysInactive()
    {
        Account account = Account.Create(new AccountParams("Inactive", "inactive-user", "inactive@example.com"));
        account.Deactivate();

        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Account stored = await context.Accounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
        Assert.False(stored.Status);
    }

    [Fact]
    public async Task Insert_NewAccount_IsActive()
    {
        Account account = Account.Create(new AccountParams("Active", "active-user", "active@example.com"));

        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Account stored = await context.Accounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
        Assert.True(stored.Status);
    }
}
