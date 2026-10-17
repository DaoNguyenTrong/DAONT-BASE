using Microsoft.EntityFrameworkCore;
using StarterKit.Application.Services.Accounts;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;
using StarterKit.Infrastructure.Persistence.Repositories;
using StarterKit.Infrastructure.Tests.TestSupport;

namespace StarterKit.Infrastructure.Tests.Persistence.Repositories;

// Commits for real (no rollback transaction): the concurrency test needs separate connections to see the
// same row. Each test owns a fresh account and deletes it afterwards.
[Collection(nameof(PostgresCollection))]
public sealed class PasswordAttemptStoreTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private static readonly DateTime Now = new(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc);

    private Account account = null!;

    public async Task InitializeAsync()
    {
        string name = $"lockout-{Guid.NewGuid():N}";
        account = Account.Create(new AccountParams("Lockout", name, $"{name}@example.com"));
        await using AppDbContext context = fixture.CreateDbContext();
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using AppDbContext context = fixture.CreateDbContext();
        await context.Accounts.Where(a => a.Id == account.Id).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task ReserveAttempt_CountsWithinTheWindow()
    {
        Assert.Equal(1, await Reserve(Now));
        Assert.Equal(2, await Reserve(Now.AddMinutes(1)));
        Assert.Equal(3, await Reserve(Now.AddMinutes(14)));
    }

    [Fact]
    public async Task ReserveAttempt_AfterTheWindow_StartsAgainAtOne()
    {
        await Reserve(Now);
        await Reserve(Now.AddMinutes(1));

        Assert.Equal(1, await Reserve(Now.AddMinutes(15)));
        Assert.Equal(2, await Reserve(Now.AddMinutes(16)));
    }

    [Fact]
    public async Task ReserveAttempt_WhileLocked_IsRefusedAndNotCounted()
    {
        await Reserve(Now);
        await WithStore(store => store.LockAsync(PasswordAttemptPurpose.Login, account.Id, Now.AddMinutes(15), CancellationToken.None));

        Assert.Null(await Reserve(Now.AddMinutes(5)));
        Account stored = await Load();
        Assert.Equal(1, stored.FailedLoginCount);
        Assert.Equal(Now.AddMinutes(15), stored.LockedUntil);
    }

    [Fact]
    public async Task ReserveAttempt_AfterTheLockExpired_StartsACleanCycle()
    {
        for (int i = 0; i < 5; i++)
        {
            await Reserve(Now.AddMinutes(i));
        }

        await WithStore(store => store.LockAsync(PasswordAttemptPurpose.Login, account.Id, Now.AddMinutes(19), CancellationToken.None));

        Assert.Equal(1, await Reserve(Now.AddMinutes(19)));
        Assert.Null((await Load()).LockedUntil);
    }

    // The point of reserving before the password check: parallel guesses each get their own number, so
    // only the first few can be verified no matter how many arrive at once.
    [Fact]
    public async Task ReserveAttempt_InParallel_GivesEveryAttemptItsOwnNumber()
    {
        int?[] attempts = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Reserve(Now)));

        Assert.Equal(Enumerable.Range(1, 20), attempts.Select(attempt => attempt ?? 0).Order());
    }

    [Fact]
    public async Task Clear_ResetsTheCounterAndLock()
    {
        await Reserve(Now);
        await WithStore(store => store.LockAsync(PasswordAttemptPurpose.Login, account.Id, Now.AddMinutes(15), CancellationToken.None));

        await WithStore(store => store.ClearAsync(PasswordAttemptPurpose.Login, account.Id, CancellationToken.None));

        Account stored = await Load();
        Assert.Equal(0, stored.FailedLoginCount);
        Assert.Null(stored.FailedLoginWindowStartedAt);
        Assert.Null(stored.LockedUntil);
    }

    // Sign-in and password change keep separate books: locking or clearing one leaves the other as it was.
    [Fact]
    public async Task Purposes_AreCountedLockedAndClearedApart()
    {
        await Reserve(Now);
        await Reserve(Now.AddMinutes(1));
        await WithStore(store => store.LockAsync(PasswordAttemptPurpose.Login, account.Id, Now.AddMinutes(15), CancellationToken.None));

        Assert.Equal(1, await Reserve(Now.AddMinutes(2), PasswordAttemptPurpose.PasswordChange));
        Assert.Null(await Reserve(Now.AddMinutes(2)));

        await WithStore(store => store.LockAsync(
            PasswordAttemptPurpose.PasswordChange, account.Id, Now.AddMinutes(20), CancellationToken.None));
        await WithStore(store => store.ClearAsync(PasswordAttemptPurpose.Login, account.Id, CancellationToken.None));

        Account stored = await Load();
        Assert.Equal(0, stored.FailedLoginCount);
        Assert.Null(stored.LockedUntil);
        Assert.Equal(1, stored.FailedPasswordChangeCount);
        Assert.Equal(Now.AddMinutes(20), stored.PasswordChangeLockedUntil);
        Assert.Null(await Reserve(Now.AddMinutes(3), PasswordAttemptPurpose.PasswordChange));
    }

    [Fact]
    public async Task PasswordChange_CountsWithinTheWindowAndClears()
    {
        Assert.Equal(1, await Reserve(Now, PasswordAttemptPurpose.PasswordChange));
        Assert.Equal(2, await Reserve(Now.AddMinutes(1), PasswordAttemptPurpose.PasswordChange));
        Assert.Equal(1, await Reserve(Now.AddMinutes(16), PasswordAttemptPurpose.PasswordChange));

        await WithStore(store => store.ClearAsync(PasswordAttemptPurpose.PasswordChange, account.Id, CancellationToken.None));

        Account stored = await Load();
        Assert.Equal(0, stored.FailedPasswordChangeCount);
        Assert.Null(stored.PasswordChangeWindowStartedAt);
        Assert.Null(stored.PasswordChangeLockedUntil);
    }

    // An ordinary save of a stale account (profile edit, password change) must not write back the lockout
    // columns it loaded before a concurrent attempt changed them.
    [Fact]
    public async Task SavingAStaleAccount_LeavesTheLockoutColumnsAlone()
    {
        Account stale = await Load();
        await Reserve(Now);
        await WithStore(store => store.LockAsync(PasswordAttemptPurpose.Login, account.Id, Now.AddMinutes(15), CancellationToken.None));
        await Reserve(Now, PasswordAttemptPurpose.PasswordChange);
        await WithStore(store => store.LockAsync(
            PasswordAttemptPurpose.PasswordChange, account.Id, Now.AddMinutes(15), CancellationToken.None));

        await using (AppDbContext context = fixture.CreateDbContext())
        {
            stale.Update(new AccountParams("Renamed", stale.Username, stale.Email));
            context.Accounts.Update(stale);
            await context.SaveChangesAsync();
        }

        Account stored = await Load();
        Assert.Equal("Renamed", stored.Name);
        Assert.Equal(1, stored.FailedLoginCount);
        Assert.Equal(Now.AddMinutes(15), stored.LockedUntil);
        Assert.Equal(1, stored.FailedPasswordChangeCount);
        Assert.Equal(Now.AddMinutes(15), stored.PasswordChangeLockedUntil);
    }

    private async Task<int?> Reserve(DateTime utcNow, PasswordAttemptPurpose purpose = PasswordAttemptPurpose.Login)
    {
        await using AppDbContext context = fixture.CreateDbContext();
        return await new PasswordAttemptStore(context).ReserveAttemptAsync(
            purpose, account.Id, utcNow, Window, CancellationToken.None);
    }

    private async Task WithStore(Func<PasswordAttemptStore, Task> action)
    {
        await using AppDbContext context = fixture.CreateDbContext();
        await action(new PasswordAttemptStore(context));
    }

    private async Task<Account> Load()
    {
        await using AppDbContext context = fixture.CreateDbContext();
        return await context.Accounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
    }
}
