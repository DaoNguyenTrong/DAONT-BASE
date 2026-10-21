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
    private readonly ConcurrentDictionary<string, long> scopeGenerations = new();

    // Per-key generation, bumped by RemoveAsync (entries exist only for removed keys): a GetOrSetAsync whose factory
    // was running when the key was removed then writes into a dead generation instead of re-publishing the stale value.
    private readonly ConcurrentDictionary<string, long> keyGenerations = new();

    public Task<T?> GetAsync<T>(string scope, string key, CancellationToken cancellationToken = default)
    {
        memoryCache.TryGetValue(PhysicalKey(scope, key), out T? value);

        return Task.FromResult(value);
    }

    public Task SetAsync<T>(
        string scope,
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        Write(PhysicalKey(scope, key), value, expiration);

        return Task.CompletedTask;
    }

    public async Task<T> GetOrSetAsync<T>(
        string scope,
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        string physicalKey = PhysicalKey(scope, key);

        // Not GetAsync + null-check: for a value-type T, "T?" erases to T, so a miss and a cached default(T)
        // (false, 0) look the same. TryGetValue's bool is the only reliable hit/miss signal for any T.
        if (memoryCache.TryGetValue(physicalKey, out T? cachedValue))
        {
            return cachedValue!;
        }

        T value = await factory(cancellationToken);

        // Write under the generation captured BEFORE the factory ran (see RedisCacheService): an
        // invalidation that lands mid-factory must strand this now-stale value, not publish it.
        Write(physicalKey, value, expiration);

        return value;
    }

    private void Write<T>(string physicalKey, T value, TimeSpan? expiration)
    {
        TimeSpan entryExpiration = CacheExpiration.Resolve(settings, expiration);
        if (entryExpiration <= TimeSpan.Zero)
        {
            return;
        }

        memoryCache.Set(physicalKey, value, entryExpiration);
    }

    public Task RemoveAsync(string scope, string key, CancellationToken cancellationToken = default)
    {
        string previous = PhysicalKey(scope, key);
        keyGenerations.AddOrUpdate(ScopedKey(scope, key), 1, (_, generation) => generation + 1);
        // Not required for correctness (nothing reads the old generation again) — just frees it now.
        memoryCache.Remove(previous);

        return Task.CompletedTask;
    }

    public Task InvalidateScopeAsync(string scope, CancellationToken cancellationToken = default)
    {
        scopeGenerations.AddOrUpdate(scope, 1, (_, generation) => generation + 1);

        return Task.CompletedTask;
    }

    // Separates scope / generation / key in the physical key so concatenation can't collide across
    // different splits of the same characters (e.g. scope "a", key "23" vs scope "a1", key "3").
    // U+0001 (SOH) is never expected to appear in a cache scope or key.
    private const char ScopeSeparator = '\u0001';

    // Splices the scope's generation into the key, so InvalidateScopeAsync drops a whole scope with one atomic
    // counter bump instead of enumerating keys — the technique Redis uses via INCR, visible to every instance.
    private string PhysicalKey(string scope, string key)
    {
        long scopeGeneration = scopeGenerations.GetOrAdd(scope, 0);
        long keyGeneration = keyGenerations.TryGetValue(ScopedKey(scope, key), out long generation) ? generation : 0;

        return $"{scope}{ScopeSeparator}{scopeGeneration}{ScopeSeparator}{keyGeneration}{ScopeSeparator}{key}";
    }

    private static string ScopedKey(string scope, string key) => $"{scope}{ScopeSeparator}{key}";
}
