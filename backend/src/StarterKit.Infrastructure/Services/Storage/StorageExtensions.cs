using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Infrastructure.Services.Storage;

namespace StarterKit.Infrastructure;

internal static class StorageExtensions
{
    internal static IServiceCollection AddStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<StoragePathGenerator>();
        services.Configure<StorageSettings>(configuration.GetSection(nameof(StorageSettings)));
        services.AddKeyedScoped<IStorageProvider, LocalFileProvider>("Local");
        services.AddScoped<IStorageService>(serviceProvider =>
        {
            StorageSettings storageSettings = serviceProvider
                .GetRequiredService<IOptions<StorageSettings>>()
                .Value;
            IStorageProvider provider = serviceProvider.GetRequiredKeyedService<IStorageProvider>(
                storageSettings.Provider);

            return new StorageService(provider);
        });

        return services;
    }
}
