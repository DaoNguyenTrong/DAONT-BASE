namespace StarterKit.Application.Common.Settings;

/// <summary>
/// Temporary lock on repeated wrong passwords. The same limits apply to sign-in and to the current-password
/// check of a password change, each counted and locked on its own (<c>PasswordAttemptPurpose</c>).
/// </summary>
public sealed class LoginLockoutSettings
{
    /// <summary>Wrong-password attempts allowed inside <see cref="WindowMinutes"/> before locking.</summary>
    public int MaxFailedAttempts { get; init; } = 5;

    /// <summary>Window the failed attempts are counted within, from the first one.</summary>
    public int WindowMinutes { get; init; } = 15;

    /// <summary>How long the account stays locked once the limit is reached.</summary>
    public int LockDurationMinutes { get; init; } = 15;
}
