using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;

namespace StarterKit.Infrastructure.Services.Caching;

internal sealed class RedisCacheService(
    IRedisStringStore redis,
    IOptions<CacheSettings> cacheOptions) : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CacheSettings settings = cacheOptions.Value;

    // Separates scope / generation / key so concatenation can't collide across different splits
    // of the same characters. U+0001 (SOH) is never expected in a cache scope or key.
    private const char ScopeSeparator = '\u0001';

    private const string KeyPrefix = "sk:cache";

    public async Task<T?> GetAsync<T>(string scope, string key, CancellationToken cancellationToken = default)
    {
        string physicalKey = await PhysicalKeyAsync(scope, key);
        RedisValue cached = await redis.StringGetAsync(physicalKey);

        if (cached.IsNullOrEmpty)
        {
            return default;
        }

        return Deserialize<T>(cached!);
    }

    public async Task SetAsync<T>(
        string scope,
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        string physicalKey = await PhysicalKeyAsync(scope, key);
        TimeSpan cacheExpiration = expiration ?? TimeSpan.FromMinutes(settings.DefaultExpirationMinutes);
        string payload = Serialize(value);

        await redis.StringSetAsync(physicalKey, payload, cacheExpiration);
    }

    public async Task<T> GetOrSetAsync<T>(
        string scope,
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        string physicalKey = await PhysicalKeyAsync(scope, key);
        RedisValue cached = await redis.StringGetAsync(physicalKey);

        // RedisValue.HasValue is the hit/miss signal — same reason MemoryCache uses TryGetValue
        // rather than a null-check: an unconstrained T with a value-type default (false, 0, ...)
        // is a legitimate cached payload and must not be treated as a miss.
        if (cached.HasValue)
        {
            return Deserialize<T>(cached!);
        }

        T value = await factory(cancellationToken);
        await SetAsync(scope, key, value, expiration, cancellationToken);

        return value;
    }

    public async Task RemoveAsync(string scope, string key, CancellationToken cancellationToken = default)
    {
        string physicalKey = await PhysicalKeyAsync(scope, key);
        await redis.KeyDeleteAsync(physicalKey);
    }

    public async Task InvalidateScopeAsync(string scope, CancellationToken cancellationToken = default)
    {
        // Atomic INCR makes the bump visible to every API instance sharing this Redis — the
        // distributed equivalent of MemoryCacheService's in-process ConcurrentDictionary counter.
        await redis.StringIncrementAsync(GenerationKey(scope));
    }

    private async Task<string> PhysicalKeyAsync(string scope, string key)
    {
        long generation = await CurrentGenerationAsync(scope);

        return $"{KeyPrefix}{ScopeSeparator}{scope}{ScopeSeparator}{generation}{ScopeSeparator}{key}";
    }

    private async Task<long> CurrentGenerationAsync(string scope)
    {
        RedisValue generation = await redis.StringGetAsync(GenerationKey(scope));

        if (generation.IsNullOrEmpty)
        {
            return 0;
        }

        return (long)generation;
    }

    private static string GenerationKey(string scope) =>
        $"{KeyPrefix}{ScopeSeparator}gen{ScopeSeparator}{scope}";

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
