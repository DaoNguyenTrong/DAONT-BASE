namespace StarterKit.Application.Common.Settings;

/// <summary>
/// Attributes written on the <c>access_token</c>/<c>refresh_token</c> cookies. The defaults are the
/// safe ones; a deployment overrides them only when its topology cannot satisfy them.
/// </summary>
public sealed class AuthCookieSettings
{
    // Fixed names, not configuration: written by the API layer, read back by the JWT bearer handler
    // in Infrastructure — both reference these so the two sides can't drift apart.
    public const string AccessTokenCookieName = "access_token";

    public const string RefreshTokenCookieName = "refresh_token";

    /// <summary>
    /// Whether the auth cookies are <c>Secure</c>; keep <c>true</c> wherever TLS terminates. <c>false</c> only for
    /// plain http on a non-<c>localhost</c> origin, where browsers drop Secure cookies (login 200, then every request
    /// 401); tokens then go in cleartext, so demo only. Requires <see cref="SameSite"/> <c>Lax</c> or <c>Strict</c>.
    /// </summary>
    public bool Secure { get; init; } = true;

    /// <summary>
    /// <c>None</c>, <c>Lax</c> or <c>Strict</c>. <c>None</c> is needed only when SPA and API are on different
    /// registrable domains; it stays the default, though the shipped Docker setup is same-site, so an existing
    /// deployment's cookie policy doesn't change silently.
    /// </summary>
    public string SameSite { get; init; } = "None";
}
