using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;

namespace StarterKit.Infrastructure.Services.Caching;

internal sealed class RedisCacheService(
    IRedisStringStore redis,
    IOptions<CacheSettings> cacheOptions,
    ILogger<RedisCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CacheSettings settings = cacheOptions.Value;

    // Separates scope / generation / key so concatenation can't collide across different splits
    // of the same characters. U+0001 (SOH) is never expected in a cache scope or key.
    private const char ScopeSeparator = '\u0001';

    private const string KeyPrefix = "sk:cache";

    // Every Redis call below degrades instead of throwing: the cache is an optimization, and
    // PermissionResolver/TenantAccessService sit on every authenticated request — a Redis outage must
    // cost latency (reads fall through to the factory), not fail the API. Factory exceptions are NOT
    // caught; only Redis' own failures are.

    public async Task<T?> GetAsync<T>(string scope, string key, CancellationToken cancellationToken = default)
    {
        try
        {
            CacheSlot slot = await SlotAsync(scope, key);
            RedisValue cached = await redis.StringGetAsync(slot.PhysicalKey);

            return cached.IsNullOrEmpty ? default : Deserialize<T>(cached!);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            logger.LogWarning(exception, "Redis cache read failed for scope {Scope}; treating as a miss", scope);
            return default;
        }
    }

    public async Task SetAsync<T>(
        string scope,
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            CacheSlot slot = await SlotAsync(scope, key);
            await WriteAsync(slot, value, expiration);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            logger.LogWarning(exception, "Redis cache write failed for scope {Scope}; value not cached", scope);
        }
    }

    public async Task<T> GetOrSetAsync<T>(
        string scope,
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        CacheSlot slot;
        try
        {
            slot = await SlotAsync(scope, key);
            RedisValue cached = await redis.StringGetAsync(slot.PhysicalKey);

            // RedisValue.HasValue is the hit/miss signal — same reason MemoryCache uses TryGetValue
            // rather than a null-check: an unconstrained T with a value-type default (false, 0, ...)
            // is a legitimate cached payload and must not be treated as a miss.
            if (cached.HasValue)
            {
                return Deserialize<T>(cached!);
            }
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            logger.LogWarning(exception, "Redis cache read failed for scope {Scope}; computing without cache", scope);
            return await factory(cancellationToken);
        }

        T value = await factory(cancellationToken);

        // Write under the generations read BEFORE the factory ran, not freshly re-read ones: if the
        // scope or this key was invalidated while the factory was computing, the value is already
        // stale and must land in an abandoned generation, where nobody will read it again.
        try
        {
            await WriteAsync(slot, value, expiration);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            logger.LogWarning(exception, "Redis cache write failed for scope {Scope}; value not cached", scope);
        }

        return value;
    }

    public async Task RemoveAsync(string scope, string key, CancellationToken cancellationToken = default)
    {
        try
        {
            CacheSlot previous = await SlotAsync(scope, key);
            // Bump the key's own generation (atomic INCR, visible to every instance) rather than only
            // deleting: a GetOrSetAsync whose factory is mid-flight would otherwise re-create the
            // just-removed entry with the stale value it computed.
            await IncrementGenerationAsync(previous.KeyCounter);
            // Not required for correctness — just frees the abandoned entry before its TTL.
            await redis.KeyDeleteAsync(previous.PhysicalKey);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            // Can't invalidate: a stale entry may be served until its TTL once Redis is back.
            logger.LogError(exception, "Redis cache invalidation failed for scope {Scope}", scope);
        }
    }

    public async Task InvalidateScopeAsync(string scope, CancellationToken cancellationToken = default)
    {
        try
        {
            // Atomic INCR makes the bump visible to every API instance sharing this Redis — the
            // distributed equivalent of MemoryCacheService's in-process ConcurrentDictionary counter.
            await IncrementGenerationAsync(ScopeGenerationKey(scope));
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            logger.LogError(exception, "Redis cache scope invalidation failed for scope {Scope}", scope);
        }
    }

    private static bool IsRedisFailure(Exception exception) =>
        exception is RedisException or TimeoutException;

    // Every counter carries a TTL longer than any entry can live (CacheExpiration.GenerationTtl), and
    // each write below pushes both counters' expiry past the entry's own. So a counter only expires once
    // every entry it versions has, and restarting it from 0 can't resurrect anything. Redis must expire
    // keys only through their TTL — maxmemory-policy noeviction; any eviction policy could drop a
    // counter early. A full Redis then rejects the INCR, which surfaces as a logged invalidation failure.
    private async Task WriteAsync<T>(CacheSlot slot, T value, TimeSpan? expiration)
    {
        TimeSpan generationTtl = CacheExpiration.GenerationTtl(settings);

        // Unconditional: a counter read as missing (generation 0) may have been created by an
        // invalidation while the factory ran, and this entry still belongs to it.
        bool scopeCounterAlive = await redis.KeyExpireAsync(slot.ScopeCounter, generationTtl);
        bool keyCounterAlive = await redis.KeyExpireAsync(slot.KeyCounter, generationTtl);

        // A counter read as > 0 that is gone now expired after the read: it restarts from 0 and may
        // climb back to the generation this value would be written under, so don't write it.
        if ((slot.ScopeGeneration > 0 && !scopeCounterAlive) || (slot.KeyGeneration > 0 && !keyCounterAlive))
        {
            return;
        }

        await redis.StringSetAsync(slot.PhysicalKey, Serialize(value), CacheExpiration.Resolve(settings, expiration));
    }

    private async Task IncrementGenerationAsync(string counter)
    {
        await redis.StringIncrementAsync(counter);
        await redis.KeyExpireAsync(counter, CacheExpiration.GenerationTtl(settings));
    }

    // Both generation counters come back in one MGET.
    private async Task<CacheSlot> SlotAsync(string scope, string key)
    {
        string scopeCounter = ScopeGenerationKey(scope);
        string keyCounter = KeyGenerationKey(scope, key);
        RedisValue[] generations = await redis.StringGetAsync([scopeCounter, keyCounter]);
        long scopeGeneration = generations[0].IsNullOrEmpty ? 0 : (long)generations[0];
        long keyGeneration = generations[1].IsNullOrEmpty ? 0 : (long)generations[1];

        string physicalKey =
            $"{KeyPrefix}{ScopeSeparator}{scope}{ScopeSeparator}{scopeGeneration}{ScopeSeparator}{keyGeneration}{ScopeSeparator}{key}";

        return new CacheSlot(physicalKey, scopeCounter, scopeGeneration, keyCounter, keyGeneration);
    }

    private readonly record struct CacheSlot(
        string PhysicalKey,
        string ScopeCounter,
        long ScopeGeneration,
        string KeyCounter,
        long KeyGeneration);

    // The scope sits in a {hash tag} in both counter keys so the MGET above stays on one Redis Cluster
    // slot; without it the cluster answers CROSSSLOT, which the outage fallback would turn into a
    // silently disabled cache.
    private static string ScopeGenerationKey(string scope) =>
        $"{KeyPrefix}{ScopeSeparator}gen{ScopeSeparator}{{{scope}}}";

    private static string KeyGenerationKey(string scope, string key) =>
        $"{KeyPrefix}{ScopeSeparator}kgen{ScopeSeparator}{{{scope}}}{ScopeSeparator}{key}";

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    private static T Deserialize<T>(string payload)
    {
        Type concreteType = ConcreteType(typeof(T));
        object? value = JsonSerializer.Deserialize(payload, concreteType, JsonOptions);

        if (value is null)
        {
            return default!;
        }

        return (T)value;
    }

    // STJ cannot materialize interface collection targets (IReadOnlySet<>, IReadOnlyDictionary<>, …)
    // — map them to concrete types that the callers actually construct (HashSet, Dictionary, List).
    private static Type ConcreteType(Type type)
    {
        if (!type.IsInterface && !type.IsAbstract)
        {
            return type;
        }

        if (!type.IsGenericType)
        {
            return type;
        }

        Type definition = type.GetGenericTypeDefinition();
        Type[] arguments = type.GetGenericArguments();

        if (definition == typeof(IReadOnlySet<>) || definition == typeof(ISet<>))
        {
            return typeof(HashSet<>).MakeGenericType(arguments);
        }

        if (definition == typeof(IReadOnlyDictionary<,>) || definition == typeof(IDictionary<,>))
        {
            return typeof(Dictionary<,>).MakeGenericType(arguments);
        }

        if (definition == typeof(IReadOnlyList<>) || definition == typeof(IList<>))
        {
            return typeof(List<>).MakeGenericType(arguments);
        }

        return type;
    }
}
