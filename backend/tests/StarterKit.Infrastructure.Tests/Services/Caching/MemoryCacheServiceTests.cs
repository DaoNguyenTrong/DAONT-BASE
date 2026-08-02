using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Settings;
using StarterKit.Infrastructure.Services.Caching;

namespace StarterKit.Infrastructure.Tests.Services.Caching;

public class MemoryCacheServiceTests
{
    private static MemoryCacheService CreateService(out IMemoryCache memoryCache)
    {
        memoryCache = new MemoryCache(new MemoryCacheOptions());
        IOptions<CacheSettings> options = Options.Create(new CacheSettings { DefaultExpirationMinutes = 5 });

        return new MemoryCacheService(memoryCache, options);
    }

    [Fact]
    public async Task GetAsync_Miss_ReturnsDefault()
    {
        MemoryCacheService service = CreateService(out _);

        string? value = await service.GetAsync<string>("missing-key");

        Assert.Null(value);
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_RoundTrips()
    {
        MemoryCacheService service = CreateService(out _);

        await service.SetAsync("key", "value");
        string? value = await service.GetAsync<string>("key");

        Assert.Equal("value", value);
    }

    [Fact]
    public async Task GetOrSetAsync_Miss_InvokesFactoryAndCaches()
    {
        MemoryCacheService service = CreateService(out _);
        int factoryCalls = 0;

        string first = await service.GetOrSetAsync("key", _ =>
        {
            factoryCalls++;
            return Task.FromResult("computed");
        });
        string second = await service.GetOrSetAsync("key", _ =>
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
        // Regression guard: for an unconstrained T instantiated with a value type, "T?" erases
        // to plain T at runtime, so default(T) (false) must never be mistaken for a cache hit.
        MemoryCacheService service = CreateService(out _);
        int factoryCalls = 0;

        bool result = await service.GetOrSetAsync("bool-key", _ =>
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
        MemoryCacheService service = CreateService(out _);
        int factoryCalls = 0;

        bool first = await service.GetOrSetAsync("bool-key-false", _ =>
        {
            factoryCalls++;
            return Task.FromResult(false);
        });
        bool second = await service.GetOrSetAsync("bool-key-false", _ =>
        {
            factoryCalls++;
            return Task.FromResult(true);
        });

        Assert.False(first);
        Assert.False(second);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task RemoveAsync_EvictsKey()
    {
        MemoryCacheService service = CreateService(out _);
        await service.SetAsync("key", "value");

        await service.RemoveAsync("key");

        Assert.Null(await service.GetAsync<string>("key"));
    }

    [Fact]
    public async Task RemoveByPrefixAsync_RemovesOnlyMatchingKeys()
    {
        MemoryCacheService service = CreateService(out _);
        await service.SetAsync("prefix:one", "1");
        await service.SetAsync("prefix:two", "2");
        await service.SetAsync("other:key", "3");

        await service.RemoveByPrefixAsync("prefix:");

        Assert.Null(await service.GetAsync<string>("prefix:one"));
        Assert.Null(await service.GetAsync<string>("prefix:two"));
        Assert.Equal("3", await service.GetAsync<string>("other:key"));
    }
}
