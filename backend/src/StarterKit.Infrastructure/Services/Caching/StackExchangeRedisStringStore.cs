using StackExchange.Redis;

namespace StarterKit.Infrastructure.Services.Caching;

internal sealed class StackExchangeRedisStringStore(IConnectionMultiplexer connectionMultiplexer)
    : IRedisStringStore
{
    private readonly IDatabase database = connectionMultiplexer.GetDatabase();

    public Task<RedisValue> StringGetAsync(string key) =>
        database.StringGetAsync(key);

    public async Task StringSetAsync(string key, string value, TimeSpan expiry) =>
        await database.StringSetAsync(key, value, expiry);

    public async Task KeyDeleteAsync(string key) =>
        await database.KeyDeleteAsync(key);

    public Task<long> StringIncrementAsync(string key) =>
        database.StringIncrementAsync(key);
}
