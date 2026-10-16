using StackExchange.Redis;

namespace StarterKit.Infrastructure.Services.Caching;

internal sealed class StackExchangeRedisStringStore(IConnectionMultiplexer connectionMultiplexer)
    : IRedisStringStore
{
    private readonly IDatabase database = connectionMultiplexer.GetDatabase();

    public Task<RedisValue> StringGetAsync(string key) =>
        database.StringGetAsync(key);

    public Task<RedisValue[]> StringGetAsync(string[] keys) =>
        database.StringGetAsync(Array.ConvertAll(keys, key => (RedisKey)key));

    public async Task KeyDeleteAsync(string key) =>
        await database.KeyDeleteAsync(key);

    // Commands queued on a transaction complete only once it executes, so their tasks are not awaited
    // individually; ExecuteAsync surfaces any failure.
    public async Task IncrementAsync(string key, TimeSpan expiry)
    {
        ITransaction transaction = database.CreateTransaction();
        _ = transaction.StringIncrementAsync(key);
        _ = transaction.KeyExpireAsync(key, expiry);
        await transaction.ExecuteAsync();
    }

    public async Task<bool> SetIfGenerationsUnchangedAsync(
        string key,
        string value,
        TimeSpan expiry,
        IReadOnlyList<GenerationGuard> guards,
        TimeSpan guardExpiry)
    {
        ITransaction transaction = database.CreateTransaction();
        foreach (GenerationGuard guard in guards)
        {
            // INCR never yields 0, so generation 0 means the counter has never been bumped (or expired).
            transaction.AddCondition(guard.Generation == 0
                ? Condition.KeyNotExists(guard.Key)
                : Condition.StringEqual(guard.Key, guard.Generation));
            _ = transaction.KeyExpireAsync(guard.Key, guardExpiry);
        }

        _ = transaction.StringSetAsync(key, value, expiry);

        return await transaction.ExecuteAsync();
    }
}
