using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;
using StarterKit.Application.Common.Settings;
using StarterKit.Infrastructure.Services.Caching;

namespace StarterKit.Infrastructure.Tests.Services.Caching;

public class RedisCacheServiceTests
{
    private static RedisCacheService CreateService(out Dictionary<string, string> store)
    {
        store = new Dictionary<string, string>();
        Dictionary<string, string> strings = store;

        IRedisStringStore redis = Substitute.For<IRedisStringStore>();

        redis.StringGetAsync(Arg.Any<string>())
            .Returns(callInfo =>
            {
                string key = callInfo.ArgAt<string>(0);
                return strings.TryGetValue(key, out string? value)
                    ? (RedisValue)value
                    : RedisValue.Null;
            });

        redis.StringSetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>())
            .Returns(callInfo =>
            {
                strings[callInfo.ArgAt<string>(0)] = callInfo.ArgAt<string>(1);
                return Task.CompletedTask;
            });

        redis.KeyDeleteAsync(Arg.Any<string>())
            .Returns(callInfo =>
            {
                strings.Remove(callInfo.ArgAt<string>(0));
                return Task.CompletedTask;
            });

        redis.StringIncrementAsync(Arg.Any<string>())
            .Returns(callInfo =>
            {
                string key = callInfo.ArgAt<string>(0);
                long current = 0;
                if (strings.TryGetValue(key, out string? raw) && long.TryParse(raw, out long parsed))
                {
                    current = parsed;
                }

                long next = current + 1;
                strings[key] = next.ToString();
                return next;
            });

        IOptions<CacheSettings> options = Options.Create(new CacheSettings { DefaultExpirationMinutes = 5 });
        return new RedisCacheService(redis, options);
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
}
