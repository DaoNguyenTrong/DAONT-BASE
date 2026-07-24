using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Services.ApiKeys;
using StarterKit.Application.Services.Accounts;
using StarterKit.Application.Services.AuditLogs;
using StarterKit.Application.Services.Auth;
using StarterKit.Application.Services.Files;
using StarterKit.Application.Services.SystemSettings;

namespace StarterKit.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration _)
    {
        services.AddScoped<IApiKeyService, ApiKeyService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();

        return services;
    }
}
