using Microsoft.Extensions.DependencyInjection;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Infrastructure.Services.Context;

namespace StarterKit.Infrastructure;

internal static class ContextExtensions
{
    internal static IServiceCollection AddContext(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IUserTimeZoneProvider, UserTimeZoneProvider>();

        return services;
    }
}
