using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;

namespace StarterKit.Infrastructure.Services.Caching;

internal sealed class MemoryCacheService(
    IMemoryCache memoryCache,
    IOptions<CacheSettings> cacheOptions) : ICacheService
{
    private readonly CacheSettings settings = cacheOptions.Value;
    private readonly ConcurrentDictionary<string, byte> keys = [];

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        memoryCache.TryGetValue(key, out T? value);

        return Task.FromResult(value);
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        TimeSpan cacheExpiration = expiration ?? TimeSpan.FromMinutes(settings.DefaultExpirationMinutes);

        MemoryCacheEntryOptions options = new()
        {
            AbsoluteExpirationRelativeToNow = cacheExpiration
        };

        options.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
        {
            if (evictedKey is string cacheKey)
            {
                keys.TryRemove(cacheKey, out _);
            }
        });

        memoryCache.Set(key, value, options);
        keys.TryAdd(key, 0);

        return Task.CompletedTask;
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        // Can't reuse GetAsync + null-check here: for an unconstrained T instantiated with a
        // value type (e.g. bool), "T?" erases to plain T at runtime, so a cache MISS and a
        // legitimately cached default(T) (false, 0, ...) are indistinguishable via nullability
        // alone. TryGetValue's own bool result is the only reliable hit/miss signal for any T.
        if (memoryCache.TryGetValue(key, out T? cachedValue))
        {
            return cachedValue!;
        }

        T value = await factory(cancellationToken);
        await SetAsync(key, value, expiration, cancellationToken);

        return value;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        memoryCache.Remove(key);
        keys.TryRemove(key, out _);

        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        IEnumerable<string> keysToRemove = keys.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.Ordinal));

        foreach (string key in keysToRemove)
        {
            memoryCache.Remove(key);
            keys.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}
