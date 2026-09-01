using Amazon.Runtime;
using Amazon.S3;
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
        services.Configure<SeaweedFsSettings>(configuration.GetSection(nameof(SeaweedFsSettings)));

        services.AddKeyedScoped<IStorageProvider, LocalFileProvider>("Local");

        // ForcePathStyle is required for any non-AWS S3-compatible endpoint (SeaweedFS, MinIO) —
        // without it the SDK builds virtual-hosted-style URLs (bucket.host) that only real AWS resolves.
        services.AddSingleton<IAmazonS3>(serviceProvider =>
        {
            SeaweedFsSettings seaweedFsSettings = serviceProvider
                .GetRequiredService<IOptions<SeaweedFsSettings>>()
                .Value;

            AmazonS3Config config = new()
            {
                ServiceURL = seaweedFsSettings.ServiceUrl,
                ForcePathStyle = true
            };

            return new AmazonS3Client(
                new BasicAWSCredentials(seaweedFsSettings.AccessKey, seaweedFsSettings.SecretKey),
                config);
        });
        services.AddKeyedScoped<IStorageProvider, SeaweedFsFileProvider>("SeaweedFS");

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
