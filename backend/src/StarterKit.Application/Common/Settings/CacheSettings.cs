namespace StarterKit.Application.Common.Settings;

public sealed class CacheSettings
{
    public string Provider { get; init; } = "Memory";

    public int DefaultExpirationMinutes { get; init; } = 5;

    /// <summary>
    /// Upper bound for any entry's TTL; longer requested expirations are clamped to it. Redis'
    /// invalidation counters expire a little after this, so it also bounds how long one survives
    /// without traffic.
    /// </summary>
    public int MaxExpirationMinutes { get; init; } = 1440;
}
