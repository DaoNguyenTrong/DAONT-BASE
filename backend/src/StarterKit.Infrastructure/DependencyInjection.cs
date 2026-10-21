using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Settings;
using StarterKit.Infrastructure.Services.Auth;

namespace StarterKit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddSecurity();
        services.AddContext();
        services.AddCaching(configuration);
        services.AddJwtAuthentication(configuration);
        services.AddStorage(configuration);
        services.AddEmail(configuration);
        services.AddPush(configuration);
        services.AddExternalAuth(configuration);
        services.AddBackgroundJobs(configuration);
        services.AddNotificationChannels();
        services.AddRealtime(configuration);
        return services;
    }

    // Recurring jobs must be scheduled against a resolved IRecurringJobManager, not the static
    // RecurringJob facade — JobStorage.Current isn't populated until the DI container is built,
    // so this can only run after app.Build(), not inside AddInfrastructure.
    public static void UseBackgroundJobs(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();
        IRecurringJobManager recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
        RefreshTokenCleanupSettings cleanupSettings = scope.ServiceProvider
            .GetRequiredService<IOptions<RefreshTokenCleanupSettings>>().Value;

        // Cronos rejects a step >= the field's range (e.g. "*/24" for hours, 0-23), so a 24h
        // interval — the default — is expressed as a fixed daily run instead of a step.
        string cronExpression = cleanupSettings.IntervalHours >= 24
            ? "0 0 * * *"
            : $"0 */{cleanupSettings.IntervalHours} * * *";

        recurringJobManager.AddOrUpdate<RefreshTokenCleanupJob>(
            "refresh-token-cleanup",
            job => job.RunAsync(CancellationToken.None),
            cronExpression);
    }

    // Bucket must exist before the first upload — the S3 gateways of SeaweedFS and MinIO do not auto-create
    // buckets on PutObject the way some S3-compatible stores do. No-op for any other provider.
    public static async Task UseStorageAsync(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();
        StorageSettings storageSettings = scope.ServiceProvider
            .GetRequiredService<IOptions<StorageSettings>>().Value;

        switch (storageSettings.Provider)
        {
            case "SeaweedFS":
                await EnsureBucketAsync(
                    scope.ServiceProvider.GetRequiredService<IAmazonS3>(),
                    scope.ServiceProvider.GetRequiredService<IOptions<SeaweedFsSettings>>().Value.BucketName);
                break;
            case StorageExtensions.MinioProvider:
                await EnsureBucketAsync(
                    scope.ServiceProvider.GetRequiredKeyedService<IAmazonS3>(StorageExtensions.MinioProvider),
                    scope.ServiceProvider.GetRequiredService<IOptions<MinioSettings>>().Value.BucketName);
                break;
        }
    }

    private static async Task EnsureBucketAsync(IAmazonS3 s3Client, string bucketName)
    {
        if (!await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, bucketName))
        {
            await s3Client.PutBucketAsync(bucketName);
        }

        // Only public/ is readable without credentials; every other key stays reachable through the API alone.
        await s3Client.PutBucketPolicyAsync(new PutBucketPolicyRequest
        {
            BucketName = bucketName,
            Policy = $$"""
                {"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":"*","Action":["s3:GetObject"],"Resource":["arn:aws:s3:::{{bucketName}}/public/*"]}]}
                """
        });
    }
}
