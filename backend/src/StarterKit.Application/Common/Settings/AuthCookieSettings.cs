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
    /// Whether the auth cookies carry the <c>Secure</c> attribute. Leave <c>true</c> wherever TLS
    /// terminates. Set <c>false</c> only for a host served over plain http from an origin other than
    /// <c>localhost</c> — the browser silently drops a Secure cookie there, so login returns 200 and
    /// every later request is 401. With it off, tokens travel in cleartext: fine for a throwaway demo,
    /// never for real data. Must be paired with <see cref="SameSite"/> <c>Lax</c> or <c>Strict</c>.
    /// </summary>
    public bool Secure { get; init; } = true;

    /// <summary>
    /// <c>None</c>, <c>Lax</c> or <c>Strict</c>. <c>None</c> is only required when the SPA and the API
    /// sit on different registrable domains. The shipped Docker setup proxies the API under the SPA's
    /// own origin (same-site), but the default stays <c>None</c> — what this API has always sent — so
    /// making it configurable doesn't silently change an existing deployment's cookie policy.
    /// </summary>
    public string SameSite { get; init; } = "None";
}
