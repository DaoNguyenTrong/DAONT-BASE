namespace StarterKit.Application.Common.Settings;

public sealed class JwtSettings
{
    public string SecretKey { get; init; } = string.Empty;

    public string Issuer { get; init; } = string.Empty;

    public string[] Audiences { get; init; } = [];

    public int AccessTokenExpiryMinutes { get; init; } = 15;

    public int RefreshTokenExpiryDays { get; init; } = 7;

    /// <summary>
    /// Window after a refresh token is rotated during which the now-revoked token
    /// is still accepted once, to absorb a near-simultaneous refresh from another
    /// browser tab sharing the same cookie. A stale token replayed after this
    /// window while its family is still active is treated as reuse and burns the
    /// whole family.
    /// </summary>
    public int RefreshTokenReuseGraceSeconds { get; init; } = 60;
}
