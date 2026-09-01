namespace StarterKit.Application.Common.Interfaces;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string scope, string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(
        string scope,
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default);

    Task<T> GetOrSetAsync<T>(
        string scope,
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(string scope, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates every entry previously stored under <paramref name="scope"/> in one call.
    /// An implementation must make this effective for every reader of the scope immediately — e.g.
    /// via an atomic per-scope generation counter — not by enumerating and evicting keys tracked only
    /// in the calling process's local state; a cache shared across multiple app instances (a
    /// distributed cache such as Redis) would not propagate a locally-tracked eviction to the others.
    /// </summary>
    Task InvalidateScopeAsync(string scope, CancellationToken cancellationToken = default);
}
