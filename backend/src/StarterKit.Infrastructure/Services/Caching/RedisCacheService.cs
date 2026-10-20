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

    // Every Redis call degrades instead of throwing: PermissionResolver/TenantAccessService run on every
    // authenticated request, so a Redis outage must cost latency (reads fall through to the factory), not fail
    // the API. Factory exceptions are NOT caught; only Redis' own failures are.

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

        // Guarded by the generations read BEFORE the factory ran, not freshly re-read ones: if the scope
        // or this key was invalidated while the factory was computing, the value is already stale and
        // the write is dropped.
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

    // Counters' TTL (CacheExpiration.GenerationTtl) outlives any entry and each write extends both in the entry's
    // transaction, so a counter expires only after everything it versions — restarting at 0 resurrects nothing.
    // Requires maxmemory-policy noeviction (eviction could drop a counter early); a full Redis fails the INCR, logged.
    private async Task WriteAsync<T>(CacheSlot slot, T value, TimeSpan? expiration)
    {
        TimeSpan entryExpiration = CacheExpiration.Resolve(settings, expiration);
        if (entryExpiration <= TimeSpan.Zero)
        {
            return;
        }

        // Commits only while both counters still hold the generations read before the factory ran: an
        // invalidation (or a counter that expired and was re-created) since then means the value may
        // already be stale, and nobody would read it back under a live generation anyway.
        await redis.SetIfGenerationsUnchangedAsync(
            slot.PhysicalKey,
            Serialize(value),
            entryExpiration,
            [new GenerationGuard(slot.ScopeCounter, slot.ScopeGeneration), new GenerationGuard(slot.KeyCounter, slot.KeyGeneration)],
            CacheExpiration.GenerationTtl(settings));
    }

    private Task IncrementGenerationAsync(string counter) =>
        redis.IncrementAsync(counter, CacheExpiration.GenerationTtl(settings));

    // Both generation counters come back in one MGET.
    private async Task<CacheSlot> SlotAsync(string scope, string key)
    {
        string scopeCounter = ScopeGenerationKey(scope);
        string keyCounter = KeyGenerationKey(scope, key);
        RedisValue[] generations = await redis.StringGetAsync([scopeCounter, keyCounter]);
        long scopeGeneration = generations[0].IsNullOrEmpty ? 0 : (long)generations[0];
        long keyGeneration = generations[1].IsNullOrEmpty ? 0 : (long)generations[1];

        string physicalKey =
            $"{KeyPrefix}{ScopeSeparator}{{{scope}}}{ScopeSeparator}{scopeGeneration}{ScopeSeparator}{keyGeneration}{ScopeSeparator}{key}";

        return new CacheSlot(physicalKey, scopeCounter, scopeGeneration, keyCounter, keyGeneration);
    }

    private readonly record struct CacheSlot(
        string PhysicalKey,
        string ScopeCounter,
        long ScopeGeneration,
        string KeyCounter,
        long KeyGeneration);

    // The scope sits in a {hash tag} in the entry key and both counter keys so the MGET above and the
    // write transaction stay on one Redis Cluster slot; without it the cluster answers CROSSSLOT, which
    // the outage fallback would turn into a silently disabled cache.
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
