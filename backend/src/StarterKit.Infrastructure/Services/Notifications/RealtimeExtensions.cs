using Microsoft.Extensions.DependencyInjection;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Infrastructure.Services.Notifications;

namespace StarterKit.Infrastructure;

internal static class RealtimeExtensions
{
    internal static IServiceCollection AddRealtime(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();

        return services;
    }
}
