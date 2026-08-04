using Microsoft.Extensions.DependencyInjection;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Infrastructure.Services.Jobs;
using StarterKit.Infrastructure.Services.Notifications;

namespace StarterKit.Infrastructure;

internal static class NotificationExtensions
{
    internal static IServiceCollection AddNotificationChannels(this IServiceCollection services)
    {
        services.AddScoped<IBackgroundJobDispatcher, HangfireJobDispatcher>();
        services.AddScoped<INotificationChannel, EmailNotificationChannel>();

        return services;
    }
}
