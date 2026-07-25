using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddSingleton<ICacheService, MemoryCacheService>();

        return services;
    }
}
