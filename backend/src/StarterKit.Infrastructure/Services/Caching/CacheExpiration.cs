using StarterKit.Application.Common.Settings;

namespace StarterKit.Infrastructure.Services.Caching;

/// <summary>Entry and generation-counter TTLs, shared so both providers clamp the same way.</summary>
internal static class CacheExpiration
{
    // Keeps a counter refreshed together with an entry of the maximum TTL strictly longer-lived than it.
    private static readonly TimeSpan GenerationMargin = TimeSpan.FromMinutes(1);

    // Clamped rather than rejected: callers pass configured TTLs on every authenticated request, and
    // the cache must not fail those requests over a misconfiguration. A zero or negative result means
    // "don't cache" — both providers skip the write.
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
        // A default above the maximum is clamped like any other TTL, not rejected: an existing
        // configuration with a long default must not stop the app from starting after an upgrade.
        if (settings.DefaultExpirationMinutes <= 0 || settings.MaxExpirationMinutes <= 0)
        {
            throw new InvalidOperationException(
                $"{nameof(CacheSettings)}:{nameof(CacheSettings.DefaultExpirationMinutes)} and "
                + $"{nameof(CacheSettings.MaxExpirationMinutes)} must be positive (got "
                + $"{settings.DefaultExpirationMinutes} and {settings.MaxExpirationMinutes}).");
        }
    }
}
