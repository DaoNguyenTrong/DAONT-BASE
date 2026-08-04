using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Infrastructure.Services.Notifications;

namespace StarterKit.Infrastructure;

internal static class PushExtensions
{
    // Push is a best-effort "hint" channel (see the notification module's architecture doc) —
    // unlike EmailSettings, an unconfigured FcmSettings must not block application startup.
    // Mirrors ExternalAuthExtensions.AddExternalAuth: only wire the channel up once credentials
    // are actually present, so every fresh clone (and the API integration test host) still boots.
    internal static IServiceCollection AddPush(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        FcmSettings fcmSettings = configuration.GetSection(nameof(FcmSettings)).Get<FcmSettings>()
            ?? new FcmSettings();

        services.Configure<FcmSettings>(configuration.GetSection(nameof(FcmSettings)));

        if (string.IsNullOrWhiteSpace(fcmSettings.ServiceAccountJson))
        {
            return services;
        }

        if (FirebaseApp.DefaultInstance is null)
        {
#pragma warning disable CS0618 // FromJson is obsolete in favor of the async CredentialFactory API;
                               // we hold the credential as a config string (not a file path), so the
                               // sync overload stays the simplest fit for this synchronous DI setup.
            GoogleCredential credential = GoogleCredential.FromJson(fcmSettings.ServiceAccountJson);
#pragma warning restore CS0618

            FirebaseApp.Create(new AppOptions { Credential = credential, ProjectId = fcmSettings.ProjectId });
        }

        services.AddScoped<IPushSender, FirebasePushSender>();
        services.AddScoped<INotificationChannel, PushNotificationChannel>();

        return services;
    }
}
