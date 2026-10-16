using StarterKit.Application.Common.Settings;

namespace StarterKit.Infrastructure.Services.Caching;

/// <summary>Entry and generation-counter TTLs, shared so both providers clamp the same way.</summary>
internal static class CacheExpiration
{
    // Covers the gap between refreshing a counter and writing the entry it guards.
    private static readonly TimeSpan GenerationMargin = TimeSpan.FromMinutes(1);

    // Clamped rather than rejected: callers pass configured TTLs on every authenticated request, and
    // the cache must not fail those requests over a misconfiguration (Validate catches its own).
    public static TimeSpan Resolve(CacheSettings settings, TimeSpan? requested)
    {
        TimeSpan expiration = requested ?? TimeSpan.FromMinutes(settings.DefaultExpirationMinutes);
        TimeSpan maximum = TimeSpan.FromMinutes(settings.MaxExpirationMinutes);

        return expiration > maximum ? maximum : expiration;
    }

    /// <summary>Longer than any entry can live, so a counter never expires before an entry it versions.</summary>
    public static TimeSpan GenerationTtl(CacheSettings settings) =>
        TimeSpan.FromMinutes(settings.MaxExpirationMinutes) + GenerationMargin;

    public static void Validate(CacheSettings settings)
    {
        if (settings.DefaultExpirationMinutes <= 0
            || settings.MaxExpirationMinutes <= 0
            || settings.DefaultExpirationMinutes > settings.MaxExpirationMinutes)
        {
            throw new InvalidOperationException(
                $"{nameof(CacheSettings)} requires 0 < {nameof(CacheSettings.DefaultExpirationMinutes)} "
                + $"<= {nameof(CacheSettings.MaxExpirationMinutes)} (got {settings.DefaultExpirationMinutes} "
                + $"and {settings.MaxExpirationMinutes}).");
        }
    }
}
