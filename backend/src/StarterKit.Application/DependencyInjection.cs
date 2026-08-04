using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Services.ApiKeys;
using StarterKit.Application.Services.Accounts;
using StarterKit.Application.Services.AuditLogs;
using StarterKit.Application.Services.Auth;
using StarterKit.Application.Services.Files;
using StarterKit.Application.Services.Notifications;
using StarterKit.Application.Services.Organizations;
using StarterKit.Application.Services.PermissionCatalog;
using StarterKit.Application.Services.Roles;
using StarterKit.Application.Services.SystemSettings;

namespace StarterKit.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration _)
    {
        services.AddScoped<IApiKeyService, ApiKeyService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<ITokenIssuer, TokenIssuer>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IOrganizationMembershipService, OrganizationMembershipService>();
        services.AddScoped<IPermissionCatalogService, PermissionCatalogService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();

        return services;
    }
}
