using StarterKit.Application.Common.Settings;

namespace StarterKit.API.Common;

/// <summary>
/// Names and attributes of the auth cookies. Writes and deletes go through the same
/// <see cref="CreateOptions"/>: a browser only overwrites a cookie when the deleting Set-Cookie
/// carries the same Path/Secure/SameSite it was written with.
/// </summary>
public static class AuthCookies
{
    public const string AccessTokenName = AuthCookieSettings.AccessTokenCookieName;

    public const string RefreshTokenName = AuthCookieSettings.RefreshTokenCookieName;

    public static CookieOptions CreateOptions(AuthCookieSettings settings) => new()
    {
        HttpOnly = true,
        Secure = settings.Secure,
        SameSite = ParseSameSite(settings.SameSite),
        Path = "/"
    };

    public static void Clear(HttpResponse response, AuthCookieSettings settings)
    {
        CookieOptions options = CreateOptions(settings);

        response.Cookies.Delete(AccessTokenName, options);
        response.Cookies.Delete(RefreshTokenName, options);
    }

    /// <summary>
    /// Called once at startup so a bad configuration stops the app instead of producing a login that
    /// returns 200 while the browser discards every cookie.
    /// </summary>
    public static void Validate(AuthCookieSettings settings)
    {
        SameSiteMode sameSite = ParseSameSite(settings.SameSite);

        if (!settings.Secure && sameSite == SameSiteMode.None)
        {
            throw new InvalidOperationException(
                $"{nameof(AuthCookieSettings)}:{nameof(AuthCookieSettings.Secure)}=false requires "
                + $"{nameof(AuthCookieSettings)}:{nameof(AuthCookieSettings.SameSite)}=Lax or Strict — "
                + "browsers reject a SameSite=None cookie that is not Secure.");
        }
    }

    /// <summary>
    /// Throws rather than falling back to a default: a typo in configuration must not quietly become
    /// a different cookie policy than the one intended.
    /// </summary>
    public static SameSiteMode ParseSameSite(string value)
    {
        // Enum.TryParse also accepts integer strings ("1", "42"); only the three names are valid here.
        bool isName = value.Length > 0 && !char.IsDigit(value[0]) && value[0] != '-';

        if (!isName
            || !Enum.TryParse(value, ignoreCase: true, out SameSiteMode mode)
            || mode == SameSiteMode.Unspecified)
        {
            throw new InvalidOperationException(
                $"Unknown {nameof(AuthCookieSettings)}:{nameof(AuthCookieSettings.SameSite)} '{value}'. "
                + "Supported values: None, Lax, Strict.");
        }

        return mode;
    }
}
