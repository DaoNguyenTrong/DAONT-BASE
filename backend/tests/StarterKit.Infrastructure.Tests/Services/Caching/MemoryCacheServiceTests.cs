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

    // MemoryCache.Set throws on a non-positive TTL; a TTL configured to 0 must mean "don't cache", not a 500.
    [Fact]
    public async Task Expiration_ZeroOrNegative_IsNotCached()
    {
        MemoryCacheService service = CreateService(out _);

        await service.SetAsync("scope", "one", "1", TimeSpan.Zero);
        string value = await service.GetOrSetAsync("scope", "two", _ => Task.FromResult("2"), TimeSpan.FromSeconds(-1));

        Assert.Equal("2", value);
        Assert.Null(await service.GetAsync<string>("scope", "one"));
        Assert.Null(await service.GetAsync<string>("scope", "two"));
    }

    [Fact]
    public async Task GetAsync_Miss_ReturnsDefault()
    {
        MemoryCacheService service = CreateService(out _);

        string? value = await service.GetAsync<string>("scope", "missing-key");

        Assert.Null(value);
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_RoundTrips()
    {
        MemoryCacheService service = CreateService(out _);

        await service.SetAsync("scope", "key", "value");
        string? value = await service.GetAsync<string>("scope", "key");

        Assert.Equal("value", value);
    }

    [Fact]
    public async Task GetOrSetAsync_Miss_InvokesFactoryAndCaches()
    {
        MemoryCacheService service = CreateService(out _);
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
        // Regression guard: for an unconstrained T instantiated with a value type, "T?" erases
        // to plain T at runtime, so default(T) (false) must never be mistaken for a cache hit.
        MemoryCacheService service = CreateService(out _);
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
        MemoryCacheService service = CreateService(out _);
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
    public async Task RemoveAsync_EvictsKey()
    {
        MemoryCacheService service = CreateService(out _);
        await service.SetAsync("scope", "key", "value");

        await service.RemoveAsync("scope", "key");

        Assert.Null(await service.GetAsync<string>("scope", "key"));
    }

    [Fact]
    public async Task InvalidateScopeAsync_RemovesOnlyMatchingScope()
    {
        MemoryCacheService service = CreateService(out _);
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
        MemoryCacheService service = CreateService(out _);
        await service.SetAsync("prefix", "one", "1");

        await service.InvalidateScopeAsync("prefix");
        await service.SetAsync("prefix", "one", "1-new");

        Assert.Equal("1-new", await service.GetAsync<string>("prefix", "one"));
    }

    [Fact]
    public async Task GetOrSetAsync_InvalidatedWhileFactoryRuns_DoesNotServeStaleValueAfterwards()
    {
        MemoryCacheService service = CreateService(out _);

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
        MemoryCacheService service = CreateService(out _);

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
        MemoryCacheService service = CreateService(out _);
        await service.SetAsync("prefix", "one", "1");
        await service.SetAsync("prefix", "two", "2");

        await service.RemoveAsync("prefix", "one");
        await service.SetAsync("prefix", "one", "1-new");

        Assert.Equal("1-new", await service.GetAsync<string>("prefix", "one"));
        Assert.Equal("2", await service.GetAsync<string>("prefix", "two"));
    }
}
