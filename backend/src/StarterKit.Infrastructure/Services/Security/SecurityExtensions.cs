using Microsoft.Extensions.DependencyInjection;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Infrastructure.Services.Security;

namespace StarterKit.Infrastructure;

internal static class SecurityExtensions
{
    internal static IServiceCollection AddSecurity(this IServiceCollection services)
    {
        services.AddScoped<ISecretProtector, DataProtectionSecretProtector>();

        return services;
    }
}
