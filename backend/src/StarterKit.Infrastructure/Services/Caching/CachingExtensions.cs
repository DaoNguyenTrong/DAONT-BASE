using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Infrastructure.Services.Caching;

namespace StarterKit.Infrastructure;

internal static class CachingExtensions
{
    internal static IServiceCollection AddCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.Configure<CacheSettings>(configuration.GetSection(nameof(CacheSettings)));

        services.AddKeyedSingleton<ICacheService, MemoryCacheService>("Memory");
        // A Redis provider registers here later: services.AddKeyedSingleton<ICacheService, RedisCacheService>("Redis");

        services.AddSingleton<ICacheService>(serviceProvider =>
        {
            CacheSettings cacheSettings = serviceProvider
                .GetRequiredService<IOptions<CacheSettings>>()
                .Value;

            return serviceProvider.GetRequiredKeyedService<ICacheService>(cacheSettings.Provider);
        });

        return services;
    }
}
