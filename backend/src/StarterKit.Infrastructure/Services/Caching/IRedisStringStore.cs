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

    Task StringSetAsync(string key, string value, TimeSpan expiry);

    Task KeyDeleteAsync(string key);

    Task<long> StringIncrementAsync(string key);

    /// <summary>EXPIRE — false when the key does not exist (nothing to expire).</summary>
    Task<bool> KeyExpireAsync(string key, TimeSpan expiry);
}
