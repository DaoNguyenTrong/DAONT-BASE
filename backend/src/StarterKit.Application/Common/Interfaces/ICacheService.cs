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
    /// Invalidates every entry under <paramref name="scope"/>, effective at once for every reader on every instance,
    /// e.g. via an atomic per-scope generation counter; never by evicting keys tracked in the calling process, which
    /// a distributed cache (Redis) shared by other instances would not see.
    /// </summary>
    Task InvalidateScopeAsync(string scope, CancellationToken cancellationToken = default);
}
