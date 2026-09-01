using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Infrastructure.Services.Caching;

namespace StarterKit.Infrastructure;

internal static class CachingExtensions
{
    internal const string MemoryProvider = "Memory";
    internal const string RedisProvider = "Redis";

    internal static IServiceCollection AddCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.Configure<CacheSettings>(configuration.GetSection(nameof(CacheSettings)));

        CacheSettings cacheSettings = configuration
            .GetSection(nameof(CacheSettings))
            .Get<CacheSettings>() ?? new CacheSettings();

        string providerKey = ResolveProviderKey(cacheSettings.Provider);

        services.AddKeyedSingleton<ICacheService, MemoryCacheService>(MemoryProvider);

        // Only register the Redis implementation when selected — MS DI validates every descriptor
        // at Build(), so a keyed RedisCacheService would otherwise demand IRedisStringStore even
        // in the default Memory mode.
        if (providerKey == RedisProvider)
        {
            RedisConnection.RequireConnectionString(
                configuration, $"{nameof(CacheSettings)}:{nameof(CacheSettings.Provider)}=Redis");
            EnsureRedisMultiplexer(services, configuration);
            services.AddSingleton<IRedisStringStore, StackExchangeRedisStringStore>();
            services.AddKeyedSingleton<ICacheService, RedisCacheService>(RedisProvider);
        }

        services.AddSingleton<ICacheService>(serviceProvider =>
        {
            CacheSettings resolved = serviceProvider
                .GetRequiredService<IOptions<CacheSettings>>()
                .Value;

            return serviceProvider.GetRequiredKeyedService<ICacheService>(ResolveProviderKey(resolved.Provider));
        });

        return services;
    }

    internal static string ResolveProviderKey(string provider)
    {
        if (string.Equals(provider, MemoryProvider, StringComparison.OrdinalIgnoreCase))
        {
            return MemoryProvider;
        }

        if (string.Equals(provider, RedisProvider, StringComparison.OrdinalIgnoreCase))
        {
            return RedisProvider;
        }

        throw new InvalidOperationException(
            $"Unknown CacheSettings:Provider '{provider}'. Supported values: {MemoryProvider}, {RedisProvider}.");
    }

    internal static void EnsureRedisMultiplexer(IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IConnectionMultiplexer)))
        {
            return;
        }

        string connectionString = RedisConnection.RequireConnectionString(
            configuration, "a Redis-backed feature");

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(connectionString));
    }
}
