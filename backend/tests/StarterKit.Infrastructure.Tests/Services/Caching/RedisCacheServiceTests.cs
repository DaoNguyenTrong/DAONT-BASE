using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;
using StarterKit.Application.Common.Settings;
using StarterKit.Infrastructure.Services.Caching;

namespace StarterKit.Infrastructure.Tests.Services.Caching;

public class RedisCacheServiceTests
{
    private const int MaxExpirationMinutes = 60;

    // Stands in for Redis: string values plus the TTL each key carries (no entry = no TTL).
    private sealed class FakeRedis
    {
        public Dictionary<string, string> Strings { get; } = new();

        public Dictionary<string, TimeSpan> Expiries { get; } = new();

        public List<string[]> MultiGets { get; } = new();

        public IEnumerable<string> CounterKeys =>
            Strings.Keys.Where(key => key.Contains("\u0001gen\u0001") || key.Contains("\u0001kgen\u0001"));

        public IEnumerable<string> EntryKeys => Strings.Keys.Except(CounterKeys);
    }

    private static RedisCacheService CreateService(out FakeRedis fake)
    {
        FakeRedis state = new();
        fake = state;

        IRedisStringStore redis = Substitute.For<IRedisStringStore>();

        redis.StringGetAsync(Arg.Any<string>())
            .Returns(callInfo =>
            {
                string key = callInfo.ArgAt<string>(0);
                return state.Strings.TryGetValue(key, out string? value)
                    ? (RedisValue)value
                    : RedisValue.Null;
            });

        redis.StringGetAsync(Arg.Do<string[]>(state.MultiGets.Add))
            .Returns(callInfo => callInfo.ArgAt<string[]>(0)
                .Select(key => state.Strings.TryGetValue(key, out string? value) ? (RedisValue)value : RedisValue.Null)
                .ToArray());

        redis.StringSetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>())
            .Returns(callInfo =>
            {
                string key = callInfo.ArgAt<string>(0);
                state.Strings[key] = callInfo.ArgAt<string>(1);
                state.Expiries[key] = callInfo.ArgAt<TimeSpan>(2);
                return Task.CompletedTask;
            });

        redis.KeyDeleteAsync(Arg.Any<string>())
            .Returns(callInfo =>
            {
                string key = callInfo.ArgAt<string>(0);
                state.Strings.Remove(key);
                state.Expiries.Remove(key);
                return Task.CompletedTask;
            });

        // INCR keeps an existing TTL and creates a missing key without one — same as Redis.
        redis.StringIncrementAsync(Arg.Any<string>())
            .Returns(callInfo =>
            {
                string key = callInfo.ArgAt<string>(0);
                long current = 0;
                if (state.Strings.TryGetValue(key, out string? raw) && long.TryParse(raw, out long parsed))
                {
                    current = parsed;
                }

                long next = current + 1;
                state.Strings[key] = next.ToString();
                return next;
            });

        // EXPIRE on a missing key is a no-op that answers false.
        redis.KeyExpireAsync(Arg.Any<string>(), Arg.Any<TimeSpan>())
            .Returns(callInfo =>
            {
                string key = callInfo.ArgAt<string>(0);
                if (!state.Strings.ContainsKey(key))
                {
                    return false;
                }

                state.Expiries[key] = callInfo.ArgAt<TimeSpan>(1);
                return true;
            });

        return new RedisCacheService(redis, Settings(), NullLogger<RedisCacheService>.Instance);
    }

    private static IOptions<CacheSettings> Settings() =>
        Options.Create(new CacheSettings { DefaultExpirationMinutes = 5, MaxExpirationMinutes = MaxExpirationMinutes });

    private static RedisCacheService CreateUnreachableService()
    {
        IRedisStringStore redis = Substitute.For<IRedisStringStore>();
        RedisConnectionException down = new(
            ConnectionFailureType.UnableToConnect, CommandFlags.None, "redis is down", null, CommandStatus.Unknown);

        redis.StringGetAsync(Arg.Any<string>()).Returns<Task<RedisValue>>(_ => throw down);
        redis.StringGetAsync(Arg.Any<string[]>()).Returns<Task<RedisValue[]>>(_ => throw down);
        redis.StringSetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns<Task>(_ => throw down);
        redis.KeyDeleteAsync(Arg.Any<string>()).Returns<Task>(_ => throw down);
        redis.StringIncrementAsync(Arg.Any<string>()).Returns<Task<long>>(_ => throw down);
        redis.KeyExpireAsync(Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns<Task<bool>>(_ => throw down);

        return new RedisCacheService(redis, Settings(), NullLogger<RedisCacheService>.Instance);
    }

    [Fact]
    public async Task RedisDown_GetOrSetAsync_FallsBackToTheFactory()
    {
        RedisCacheService service = CreateUnreachableService();

        // Cache is an optimization: an outage must cost latency, not take down every
        // authenticated request (PermissionResolver/TenantAccessService sit on this path).
        string value = await service.GetOrSetAsync("scope", "key", _ => Task.FromResult("from-db"));

        Assert.Equal("from-db", value);
    }

    [Fact]
    public async Task RedisDown_GetAsync_IsAMiss_AndWritesDoNotThrow()
    {
        RedisCacheService service = CreateUnreachableService();

        Assert.Null(await service.GetAsync<string>("scope", "key"));
        await service.SetAsync("scope", "key", "value");
        await service.RemoveAsync("scope", "key");
        await service.InvalidateScopeAsync("scope");
    }

    [Fact]
    public async Task RedisDown_FactoryException_StillPropagates()
    {
        RedisCacheService service = CreateUnreachableService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetOrSetAsync<string>("scope", "key", _ => throw new InvalidOperationException("db")));
    }

    [Fact]
    public async Task GetAsync_Miss_ReturnsDefault()
    {
        RedisCacheService service = CreateService(out _);

        string? value = await service.GetAsync<string>("scope", "missing-key");

        Assert.Null(value);
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_RoundTrips()
    {
        RedisCacheService service = CreateService(out _);

        await service.SetAsync("scope", "key", "value");
        string? value = await service.GetAsync<string>("scope", "key");

        Assert.Equal("value", value);
    }

    [Fact]
    public async Task GetOrSetAsync_Miss_InvokesFactoryAndCaches()
    {
        RedisCacheService service = CreateService(out _);
        int factoryCalls = 0;

        string first = await service.GetOrSetAsync("scope", "key", _ =>
        {
            factoryCalls++;
            return Task.FromResult("computed");
        });
        string second = await service.GetOrSetAsync("scope", "key", _ =>
        {
            factoryCalls++;
            return Task.FromResult("computed-again");
        });

        Assert.Equal("computed", first);
        Assert.Equal("computed", second);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task GetOrSetAsync_Miss_ValueTypeFalse_StillInvokesFactory()
    {
        RedisCacheService service = CreateService(out _);
        int factoryCalls = 0;

        bool result = await service.GetOrSetAsync("scope", "bool-key", _ =>
        {
            factoryCalls++;
            return Task.FromResult(true);
        });

        Assert.True(result);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task GetOrSetAsync_CachedValueTypeFalse_ReturnsCachedFalseWithoutRecalling()
    {
        RedisCacheService service = CreateService(out _);
        int factoryCalls = 0;

        bool first = await service.GetOrSetAsync("scope", "bool-key-false", _ =>
        {
            factoryCalls++;
            return Task.FromResult(false);
        });
        bool second = await service.GetOrSetAsync("scope", "bool-key-false", _ =>
        {
            factoryCalls++;
            return Task.FromResult(true);
        });

        Assert.False(first);
        Assert.False(second);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task GetOrSetAsync_IReadOnlySet_RoundTrips()
    {
        RedisCacheService service = CreateService(out _);

        IReadOnlySet<string> first = await service.GetOrSetAsync(
            "scope",
            "perms",
            _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { "a", "b" }));
        IReadOnlySet<string> second = await service.GetOrSetAsync(
            "scope",
            "perms",
            _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { "should-not-run" }));

        Assert.Equal(first.OrderBy(x => x), second.OrderBy(x => x));
        Assert.Contains("a", second);
        Assert.Contains("b", second);
    }

    [Fact]
    public async Task RemoveAsync_EvictsKey()
    {
        RedisCacheService service = CreateService(out _);
        await service.SetAsync("scope", "key", "value");

        await service.RemoveAsync("scope", "key");

        Assert.Null(await service.GetAsync<string>("scope", "key"));
    }

    [Fact]
    public async Task InvalidateScopeAsync_RemovesOnlyMatchingScope()
    {
        RedisCacheService service = CreateService(out _);
        await service.SetAsync("prefix", "one", "1");
        await service.SetAsync("prefix", "two", "2");
        await service.SetAsync("other", "key", "3");

        await service.InvalidateScopeAsync("prefix");

        Assert.Null(await service.GetAsync<string>("prefix", "one"));
        Assert.Null(await service.GetAsync<string>("prefix", "two"));
        Assert.Equal("3", await service.GetAsync<string>("other", "key"));
    }

    [Fact]
    public async Task InvalidateScopeAsync_ThenSetAsync_NewEntryIsVisibleAgain()
    {
        RedisCacheService service = CreateService(out _);
        await service.SetAsync("prefix", "one", "1");

        await service.InvalidateScopeAsync("prefix");
        await service.SetAsync("prefix", "one", "1-new");

        Assert.Equal("1-new", await service.GetAsync<string>("prefix", "one"));
    }

    [Fact]
    public async Task GetOrSetAsync_InvalidatedWhileFactoryRuns_DoesNotServeStaleValueAfterwards()
    {
        RedisCacheService service = CreateService(out _);

        // The factory reads state that a concurrent writer changes and then invalidates the
        // scope before the factory returns — the value it computed is already stale.
        string value = await service.GetOrSetAsync("prefix", "one", async _ =>
        {
            await service.InvalidateScopeAsync("prefix");
            return "stale";
        });

        Assert.Equal("stale", value);
        Assert.Null(await service.GetAsync<string>("prefix", "one"));
    }

    [Fact]
    public async Task GetOrSetAsync_RemovedWhileFactoryRuns_DoesNotServeStaleValueAfterwards()
    {
        RedisCacheService service = CreateService(out _);

        // e.g. a member's role is revoked (RemoveAsync on that member's key) while another request
        // is still loading the pre-revocation permission set for the same key.
        string value = await service.GetOrSetAsync("prefix", "one", async _ =>
        {
            await service.RemoveAsync("prefix", "one");
            return "stale";
        });

        Assert.Equal("stale", value);
        Assert.Null(await service.GetAsync<string>("prefix", "one"));
    }

    [Fact]
    public async Task RemoveAsync_ThenSetAsync_NewEntryIsVisibleAndSiblingsUntouched()
    {
        RedisCacheService service = CreateService(out _);
        await service.SetAsync("prefix", "one", "1");
        await service.SetAsync("prefix", "two", "2");

        await service.RemoveAsync("prefix", "one");
        await service.SetAsync("prefix", "one", "1-new");

        Assert.Equal("1-new", await service.GetAsync<string>("prefix", "one"));
        Assert.Equal("2", await service.GetAsync<string>("prefix", "two"));
    }

    // Redis Cluster rejects a multi-key command whose keys hash to different slots (CROSSSLOT), and the
    // outage fallback would turn that into a silently disabled cache. Keys sharing a {hash tag} share a slot.
    [Fact]
    public async Task MultiKeyReads_AllKeysShareOneHashTag()
    {
        RedisCacheService service = CreateService(out FakeRedis fake);

        await service.GetOrSetAsync("permissions:org-1", "account-1", _ => Task.FromResult("value"));
        await service.RemoveAsync("permissions:org-1", "account-1");
        await service.GetOrSetAsync("permissions:org-1", "account-1", _ => Task.FromResult("value"));

        Assert.NotEmpty(fake.MultiGets);
        Assert.All(fake.MultiGets, keys => Assert.Single(keys.Select(HashTag).Distinct()));
    }

    // Under noeviction a key only disappears through its TTL, so nothing in the cache may be left
    // without one — not even the generation counters, or every RemoveAsync would leak one forever.
    [Fact]
    public async Task EveryKeyWritten_CarriesATtl()
    {
        RedisCacheService service = CreateService(out FakeRedis fake);

        await service.SetAsync("prefix", "one", "1");
        await service.RemoveAsync("prefix", "one");
        await service.InvalidateScopeAsync("prefix");
        await service.GetOrSetAsync("prefix", "one", _ => Task.FromResult("1-new"));

        Assert.Equal(2, fake.CounterKeys.Count());
        Assert.All(fake.Strings.Keys, key => Assert.True(fake.Expiries.ContainsKey(key), $"no TTL on {key}"));
    }

    // Most removed keys are never read again, so no later write would come along to give their
    // counter a TTL — the invalidation itself has to.
    [Fact]
    public async Task Invalidation_WithoutALaterWrite_LeavesNoCounterWithoutATtl()
    {
        RedisCacheService service = CreateService(out FakeRedis fake);

        await service.RemoveAsync("prefix", "one");
        await service.InvalidateScopeAsync("prefix");

        Assert.Equal(2, fake.CounterKeys.Count());
        Assert.All(fake.CounterKeys, counter => Assert.True(fake.Expiries.ContainsKey(counter), $"no TTL on {counter}"));
    }

    // A counter that expired while an entry of its generation was still alive would restart from 0
    // and could climb back to that generation, resurrecting the entry: each write must push the
    // counters' expiry past its own.
    [Fact]
    public async Task Write_ExtendsBothCountersPastTheEntry()
    {
        RedisCacheService service = CreateService(out FakeRedis fake);
        await service.RemoveAsync("prefix", "one");
        await service.InvalidateScopeAsync("prefix");
        foreach (string counter in fake.CounterKeys)
        {
            fake.Expiries[counter] = TimeSpan.FromSeconds(1); // nearly expired
        }

        await service.SetAsync("prefix", "one", "1", TimeSpan.FromMinutes(MaxExpirationMinutes));

        TimeSpan entryTtl = fake.Expiries[Assert.Single(fake.EntryKeys)];
        Assert.All(fake.CounterKeys, counter => Assert.True(fake.Expiries[counter] > entryTtl));
    }

    // The value was read as generation 0 (no counter yet) and an invalidation created the counter while
    // the factory ran: the stale entry lands in generation 0, so the counter must still be extended.
    [Fact]
    public async Task Write_ExtendsACounterCreatedWhileTheFactoryRan()
    {
        RedisCacheService service = CreateService(out FakeRedis fake);

        await service.GetOrSetAsync("prefix", "one", async _ =>
        {
            await service.InvalidateScopeAsync("prefix");
            fake.Expiries[fake.CounterKeys.Single()] = TimeSpan.FromSeconds(1); // the factory ran long
            return "stale";
        }, TimeSpan.FromMinutes(MaxExpirationMinutes));

        TimeSpan entryTtl = fake.Expiries[Assert.Single(fake.EntryKeys)];
        Assert.True(fake.Expiries[fake.CounterKeys.Single()] > entryTtl);
    }

    [Fact]
    public async Task GetOrSetAsync_CounterExpiredWhileFactoryRan_DoesNotWrite()
    {
        RedisCacheService service = CreateService(out FakeRedis fake);
        await service.InvalidateScopeAsync("prefix");

        // Read under generation 1; the counter then expires, so the next invalidation restarts it at
        // 1 — an entry written now under generation 1 would be served again after that invalidation.
        string value = await service.GetOrSetAsync("prefix", "one", _ =>
        {
            string counter = fake.CounterKeys.Single();
            fake.Strings.Remove(counter);
            fake.Expiries.Remove(counter);
            return Task.FromResult("stale");
        });
        await service.InvalidateScopeAsync("prefix");

        Assert.Equal("stale", value);
        Assert.Null(await service.GetAsync<string>("prefix", "one"));
    }

    [Fact]
    public async Task Expiration_AboveTheMaximum_IsClamped()
    {
        RedisCacheService service = CreateService(out FakeRedis fake);

        await service.SetAsync("prefix", "one", "1", TimeSpan.FromDays(7));

        Assert.Equal(TimeSpan.FromMinutes(MaxExpirationMinutes), fake.Expiries[Assert.Single(fake.EntryKeys)]);
    }

    // Per the Redis Cluster spec: the substring between the first '{' and the next '}', when non-empty.
    private static string HashTag(string key)
    {
        int open = key.IndexOf('{');
        int close = open < 0 ? -1 : key.IndexOf('}', open + 1);
        return close > open + 1 ? key[(open + 1)..close] : key;
    }
}
