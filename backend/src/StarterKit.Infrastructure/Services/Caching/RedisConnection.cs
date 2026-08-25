using Microsoft.Extensions.Configuration;

namespace StarterKit.Infrastructure.Services.Caching;

internal static class RedisConnection
{
    internal const string ConnectionStringName = "Redis";

    internal static string? GetConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString(ConnectionStringName);

    internal static string RequireConnectionString(IConfiguration configuration, string featureName)
    {
        string? connectionString = GetConnectionString(configuration);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} is required when {featureName} uses Redis.");
        }

        return connectionString;
    }
}
