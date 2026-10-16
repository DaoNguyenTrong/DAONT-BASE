using Microsoft.EntityFrameworkCore;
using StarterKit.Application.Services.Accounts;
using StarterKit.Domain.Entities;

namespace StarterKit.Infrastructure.Persistence.Repositories;

internal sealed class PasswordAttemptStore(AppDbContext context) : IPasswordAttemptStore
{
    public async Task<int?> ReserveAttemptAsync(
        PasswordAttemptPurpose purpose,
        Guid accountId,
        DateTime utcNow,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        (string count, string windowStartedAt, string lockedUntil) = Columns(purpose);
        DateTime windowCutoff = utcNow - window;

        // One UPDATE: Postgres row-locks the account for its duration, so concurrent attempts are counted one
        // after another. The WHERE skips a running lock; a non-null lock that passes it has expired and starts
        // a clean cycle, like an elapsed window. The column names come from Columns() — constants, never input.
        List<int> attempt = await context.Database
            .SqlQueryRaw<int>(
                $$"""
                UPDATE accounts SET
                    "{{count}}" = CASE
                        WHEN "{{windowStartedAt}}" IS NULL OR "{{windowStartedAt}}" <= {0}
                            OR "{{lockedUntil}}" IS NOT NULL THEN 1
                        ELSE "{{count}}" + 1 END,
                    "{{windowStartedAt}}" = CASE
                        WHEN "{{windowStartedAt}}" IS NULL OR "{{windowStartedAt}}" <= {0}
                            OR "{{lockedUntil}}" IS NOT NULL THEN {1}
                        ELSE "{{windowStartedAt}}" END,
                    "{{lockedUntil}}" = NULL
                WHERE "Id" = {2} AND ("{{lockedUntil}}" IS NULL OR "{{lockedUntil}}" <= {1})
                RETURNING "{{count}}" AS "Value"
                """,
                windowCutoff, utcNow, accountId)
            .ToListAsync(cancellationToken);

        return attempt.Count == 0 ? null : attempt[0];
    }

    public Task LockAsync(
        PasswordAttemptPurpose purpose, Guid accountId, DateTime lockedUntil, CancellationToken cancellationToken) =>
        purpose switch
        {
            PasswordAttemptPurpose.Login => Accounts(accountId).ExecuteUpdateAsync(
                set => set.SetProperty(account => account.LockedUntil, lockedUntil), cancellationToken),
            PasswordAttemptPurpose.PasswordChange => Accounts(accountId).ExecuteUpdateAsync(
                set => set.SetProperty(account => account.PasswordChangeLockedUntil, lockedUntil), cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null)
        };

    public Task ClearAsync(PasswordAttemptPurpose purpose, Guid accountId, CancellationToken cancellationToken) =>
        purpose switch
        {
            PasswordAttemptPurpose.Login => Accounts(accountId)
                .Where(account => account.FailedLoginCount != 0 || account.LockedUntil != null)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(account => account.FailedLoginCount, 0)
                        .SetProperty(account => account.FailedLoginWindowStartedAt, (DateTime?)null)
                        .SetProperty(account => account.LockedUntil, (DateTime?)null),
                    cancellationToken),
            PasswordAttemptPurpose.PasswordChange => Accounts(accountId)
                .Where(account => account.FailedPasswordChangeCount != 0 || account.PasswordChangeLockedUntil != null)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(account => account.FailedPasswordChangeCount, 0)
                        .SetProperty(account => account.PasswordChangeWindowStartedAt, (DateTime?)null)
                        .SetProperty(account => account.PasswordChangeLockedUntil, (DateTime?)null),
                    cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null)
        };

    // Column names match AccountConfiguration (EF's default: the property name).
    private static (string Count, string WindowStartedAt, string LockedUntil) Columns(PasswordAttemptPurpose purpose) =>
        purpose switch
        {
            PasswordAttemptPurpose.Login =>
                ("FailedLoginCount", "FailedLoginWindowStartedAt", "LockedUntil"),
            PasswordAttemptPurpose.PasswordChange =>
                ("FailedPasswordChangeCount", "PasswordChangeWindowStartedAt", "PasswordChangeLockedUntil"),
            _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null)
        };

    private IQueryable<Account> Accounts(Guid accountId) =>
        context.Accounts.Where(account => account.Id == accountId);
}
