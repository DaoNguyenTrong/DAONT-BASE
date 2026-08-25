using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Infrastructure.Services.Caching;
using StarterKit.Infrastructure.Services.Notifications;

namespace StarterKit.Infrastructure;

internal static class RealtimeExtensions
{
    internal const string NoneBackplane = "None";
    internal const string RedisBackplane = "Redis";

    internal static IServiceCollection AddRealtime(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RealtimeSettings>(configuration.GetSection(nameof(RealtimeSettings)));

        RealtimeSettings realtimeSettings = configuration
            .GetSection(nameof(RealtimeSettings))
            .Get<RealtimeSettings>() ?? new RealtimeSettings();

        string backplane = ResolveBackplane(realtimeSettings.Backplane);

        if (backplane == RedisBackplane)
        {
            string connectionString = RedisConnection.RequireConnectionString(
                configuration, $"{nameof(RealtimeSettings)}:{nameof(RealtimeSettings.Backplane)}=Redis");

            CachingExtensions.EnsureRedisMultiplexer(services, configuration);

            services.AddSignalR().AddStackExchangeRedis(connectionString, options =>
            {
                options.Configuration.ChannelPrefix = RedisChannel.Literal("StarterKit");
            });
        }
        else
        {
            services.AddSignalR();
        }

        services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();

        return services;
    }

    internal static string ResolveBackplane(string backplane)
    {
        if (string.Equals(backplane, NoneBackplane, StringComparison.OrdinalIgnoreCase))
        {
            return NoneBackplane;
        }

        if (string.Equals(backplane, RedisBackplane, StringComparison.OrdinalIgnoreCase))
        {
            return RedisBackplane;
        }

        throw new InvalidOperationException(
            $"Unknown RealtimeSettings:Backplane '{backplane}'. Supported values: {NoneBackplane}, {RedisBackplane}.");
    }
}
