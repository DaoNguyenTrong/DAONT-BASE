using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Services.ApiKeys;
using FeedbackHub.Application.Services.Accounts;
using FeedbackHub.Application.Services.AuditLogs;
using FeedbackHub.Application.Services.Auth;
using FeedbackHub.Application.Services.Files;
using FeedbackHub.Application.Services.SystemSettings;
using FeedbackHub.Application.Services.Tenants;

namespace FeedbackHub.Application;

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
        services.AddScoped<ITenantService, TenantService>();

        return services;
    }
}
