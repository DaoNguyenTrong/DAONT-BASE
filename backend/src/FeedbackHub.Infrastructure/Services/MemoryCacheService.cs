using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;

namespace FeedbackHub.Infrastructure.Services;

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
        T? cachedValue = await GetAsync<T>(key, cancellationToken);

        if (cachedValue is not null)
        {
            return cachedValue;
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
