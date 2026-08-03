using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Infrastructure.Services.Auth.External;

namespace StarterKit.Infrastructure;

internal static class ExternalAuthExtensions
{
    internal static IServiceCollection AddExternalAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ExternalAuthSettings externalAuthSettings = configuration
            .GetSection(nameof(ExternalAuthSettings)).Get<ExternalAuthSettings>()
            ?? new ExternalAuthSettings();

        services.Configure<ExternalAuthSettings>(configuration.GetSection(nameof(ExternalAuthSettings)));

        // Each provider is optional — only register it once its credentials are configured, so
        // an unconfigured provider simply isn't offered (ExternalLoginProviderNotSupported)
        // instead of blocking application startup for everyone.
        if (!string.IsNullOrWhiteSpace(externalAuthSettings.Google.ClientId))
        {
            services.AddScoped<IGoogleJwtValidator, GoogleJwtValidator>();
            services.AddScoped<IExternalAuthProvider, GoogleAuthProvider>();
        }

        if (!string.IsNullOrWhiteSpace(externalAuthSettings.Microsoft.ClientId))
        {
            services.AddScoped<IMicrosoftJwtValidator, MicrosoftJwtValidator>();
            services.AddScoped<IExternalAuthProvider, MicrosoftAuthProvider>();
        }

        return services;
    }
}
