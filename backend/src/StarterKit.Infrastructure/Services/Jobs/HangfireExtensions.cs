using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.Application.Common.Settings;
using StarterKit.Infrastructure.Services.Auth;

namespace StarterKit.Infrastructure;

internal static class HangfireExtensions
{
    internal static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection connection string is missing.");

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                c => c.UseNpgsqlConnection(connectionString),
                new PostgreSqlStorageOptions { SchemaName = "hangfire" }));

        services.AddHangfireServer();

        services.Configure<RefreshTokenCleanupSettings>(
            configuration.GetSection(nameof(RefreshTokenCleanupSettings)));
        services.AddScoped<RefreshTokenCleanupJob>();

        return services;
    }
}
