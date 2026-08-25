using Amazon.S3;
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
        services.AddRealtime();
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

    // Bucket must exist before the first upload — SeaweedFS's S3 gateway does not auto-create
    // buckets on PutObject the way some S3-compatible stores do. No-op for any other provider.
    public static async Task UseStorageAsync(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();
        StorageSettings storageSettings = scope.ServiceProvider
            .GetRequiredService<IOptions<StorageSettings>>().Value;

        if (storageSettings.Provider != "SeaweedFS")
        {
            return;
        }

        IAmazonS3 s3Client = scope.ServiceProvider.GetRequiredService<IAmazonS3>();
        SeaweedFsSettings seaweedFsSettings = scope.ServiceProvider
            .GetRequiredService<IOptions<SeaweedFsSettings>>().Value;

        if (!await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, seaweedFsSettings.BucketName))
        {
            await s3Client.PutBucketAsync(seaweedFsSettings.BucketName);
        }
    }
}
