using StackExchange.Redis;

namespace StarterKit.Infrastructure.Services.Caching;

/// <summary>
/// Narrow Redis string/key surface used by <see cref="RedisCacheService"/> — keeps the cache
/// implementation free of the full <see cref="IDatabase"/> surface so unit tests can fake it
/// without fighting StackExchange.Redis overload resolution under NSubstitute.
/// </summary>
internal interface IRedisStringStore
{
    Task<RedisValue> StringGetAsync(string key);

    /// <summary>MGET — several keys in one round trip; values come back in the order requested.</summary>
    Task<RedisValue[]> StringGetAsync(string[] keys);

    Task KeyDeleteAsync(string key);

    /// <summary>INCR, then EXPIRE, as one transaction — the counter can't be left without a TTL.</summary>
    Task IncrementAsync(string key, TimeSpan expiry);

    /// <summary>
    /// SET <paramref name="key"/> and EXPIRE every guard counter, as one transaction that only commits while
    /// each guard still holds the generation it was read at (0 = the counter doesn't exist). Returns
    /// whether it committed. All keys must share one Redis Cluster hash slot.
    /// </summary>
    Task<bool> SetIfGenerationsUnchangedAsync(
        string key,
        string value,
        TimeSpan expiry,
        IReadOnlyList<GenerationGuard> guards,
        TimeSpan guardExpiry);
}

internal readonly record struct GenerationGuard(string Key, long Generation);
