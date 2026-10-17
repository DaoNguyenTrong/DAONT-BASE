using StackExchange.Redis;
using StarterKit.Infrastructure.Services.Caching;
using Testcontainers.Redis;

namespace StarterKit.Infrastructure.Tests.Services.Caching;

/// <summary>
/// The transactions behind the cache's correctness run against a real Redis: the unit tests' fake only
/// mirrors this contract, so this is where the contract itself is checked.
/// </summary>
public sealed class StackExchangeRedisStringStoreTests(StackExchangeRedisStringStoreTests.RedisFixture redis)
    : IClassFixture<StackExchangeRedisStringStoreTests.RedisFixture>
{
    private static readonly TimeSpan EntryTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CounterTtl = TimeSpan.FromMinutes(10);

    private readonly StackExchangeRedisStringStore store = new(redis.Multiplexer);
    private readonly IDatabase database = redis.Multiplexer.GetDatabase();

    // One container for the class; each test gets its own hash tag so tests can't see each other's keys.
    private readonly string tag = $"{{{Guid.NewGuid():N}}}";

    public sealed class RedisFixture : IAsyncLifetime
    {
        private readonly RedisContainer container = new RedisBuilder("redis:8-alpine").Build();

        public ConnectionMultiplexer Multiplexer { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await container.StartAsync();
            Multiplexer = await ConnectionMultiplexer.ConnectAsync(container.GetConnectionString());
        }

        public async Task DisposeAsync()
        {
            await Multiplexer.DisposeAsync();
            await container.DisposeAsync();
        }
    }

    private string Key(string name) => tag + name;

    [Fact]
    public async Task IncrementAsync_GivesTheCounterATtl()
    {
        await store.IncrementAsync(Key("counter"), CounterTtl);
        await store.IncrementAsync(Key("counter"), CounterTtl);

        Assert.Equal(2, (long)await database.StringGetAsync(Key("counter")));
        Assert.InRange((await database.KeyTimeToLiveAsync(Key("counter")))!.Value, CounterTtl - TimeSpan.FromSeconds(5), CounterTtl);
    }

    [Fact]
    public async Task SetIfGenerationsUnchanged_GuardsAsRead_WritesAndExtendsTheCounters()
    {
        await store.IncrementAsync(Key("scope"), TimeSpan.FromSeconds(30));

        bool committed = await store.SetIfGenerationsUnchangedAsync(
            Key("entry"), "value", EntryTtl, [new(Key("scope"), 1), new(Key("key"), 0)], CounterTtl);

        Assert.True(committed);
        Assert.Equal("value", (string?)await database.StringGetAsync(Key("entry")));
        Assert.True((await database.KeyTimeToLiveAsync(Key("scope")))!.Value > EntryTtl);
        Assert.False(await database.KeyExistsAsync(Key("key"))); // EXPIRE on a missing guard creates nothing
    }

    [Fact]
    public async Task SetIfGenerationsUnchanged_CounterBumped_DoesNotWrite()
    {
        await store.IncrementAsync(Key("scope"), CounterTtl);
        await store.IncrementAsync(Key("scope"), CounterTtl);

        bool committed = await store.SetIfGenerationsUnchangedAsync(
            Key("entry"), "stale", EntryTtl, [new(Key("scope"), 1)], CounterTtl);

        Assert.False(committed);
        Assert.False(await database.KeyExistsAsync(Key("entry")));
    }

    [Fact]
    public async Task SetIfGenerationsUnchanged_CounterCreatedSinceRead_DoesNotWrite()
    {
        await store.IncrementAsync(Key("scope"), CounterTtl);

        bool committed = await store.SetIfGenerationsUnchangedAsync(
            Key("entry"), "stale", EntryTtl, [new(Key("scope"), 0)], CounterTtl);

        Assert.False(committed);
        Assert.False(await database.KeyExistsAsync(Key("entry")));
    }

    [Fact]
    public async Task SetIfGenerationsUnchanged_CounterGoneSinceRead_DoesNotWrite()
    {
        bool committed = await store.SetIfGenerationsUnchangedAsync(
            Key("entry"), "stale", EntryTtl, [new(Key("scope"), 3)], CounterTtl);

        Assert.False(committed);
        Assert.False(await database.KeyExistsAsync(Key("entry")));
    }
}
