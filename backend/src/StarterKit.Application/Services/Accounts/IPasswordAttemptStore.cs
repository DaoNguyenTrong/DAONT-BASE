namespace StarterKit.Application.Services.Accounts;

/// <summary>
/// Bookkeeping for the temporary lock on repeated wrong passwords. Each call is one statement touching only the
/// lockout columns of <paramref name="purpose"/>: a load-modify-save of the account would span the ~0.3s password
/// hash and could write a stale row over a concurrent password change or deactivation.
/// </summary>
public interface IPasswordAttemptStore
{
    /// <summary>
    /// Counts one password attempt <b>before</b> it is verified, so parallel guesses can't all read the same
    /// count and slip past the limit. A window that has elapsed, or an expired lock, starts a fresh one at 1.
    /// Returns the attempt's number inside the current window, or <c>null</c> while that purpose is locked.
    /// </summary>
    Task<int?> ReserveAttemptAsync(
        PasswordAttemptPurpose purpose, Guid accountId, DateTime utcNow, TimeSpan window, CancellationToken cancellationToken);

    Task LockAsync(PasswordAttemptPurpose purpose, Guid accountId, DateTime lockedUntil, CancellationToken cancellationToken);

    /// <summary>Resets the counter after the password was right.</summary>
    Task ClearAsync(PasswordAttemptPurpose purpose, Guid accountId, CancellationToken cancellationToken);
}
