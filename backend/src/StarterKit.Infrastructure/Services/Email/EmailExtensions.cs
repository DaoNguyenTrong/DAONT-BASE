using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Infrastructure.Services.Email;

namespace StarterKit.Infrastructure;

internal static class EmailExtensions
{
    internal static IServiceCollection AddEmail(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        EmailSettings emailSettings = configuration.GetSection(nameof(EmailSettings)).Get<EmailSettings>()
            ?? throw new InvalidOperationException("EmailSettings configuration is missing.");

        if (string.IsNullOrWhiteSpace(emailSettings.Host))
            throw new InvalidOperationException("EmailSettings:Host is required.");

        if (emailSettings.Port <= 0)
            throw new InvalidOperationException("EmailSettings:Port must be a positive number.");

        if (string.IsNullOrWhiteSpace(emailSettings.FromAddress))
            throw new InvalidOperationException("EmailSettings:FromAddress is required.");

        if (string.IsNullOrWhiteSpace(emailSettings.FrontendBaseUrl))
            throw new InvalidOperationException("EmailSettings:FrontendBaseUrl is required.");

        services.Configure<EmailSettings>(configuration.GetSection(nameof(EmailSettings)));
        services.AddScoped<ISmtpClientFactory, SmtpClientFactory>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        return services;
    }
}
