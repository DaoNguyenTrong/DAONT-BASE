using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Infrastructure.Services.Auth;

namespace StarterKit.Infrastructure;

internal static class AuthExtensions
{
    internal static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        JwtSettings jwtSettings = configuration.GetSection(nameof(JwtSettings)).Get<JwtSettings>()
            ?? throw new InvalidOperationException("JwtSettings configuration is missing.");

        if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey) || jwtSettings.SecretKey.Length < 32)
            throw new InvalidOperationException("JwtSettings:SecretKey must be at least 32 characters.");

        if (string.IsNullOrWhiteSpace(jwtSettings.Issuer))
            throw new InvalidOperationException("JwtSettings:Issuer is required.");

        if (jwtSettings.Audiences.Length == 0)
            throw new InvalidOperationException("JwtSettings:Audiences must contain at least one audience.");

        services.Configure<JwtSettings>(configuration.GetSection(nameof(JwtSettings)));
        services.Configure<TenantAccessSettings>(configuration.GetSection(nameof(TenantAccessSettings)));
        services.Configure<PermissionResolverSettings>(configuration.GetSection(nameof(PermissionResolverSettings)));
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICurrentTenantProvider, CurrentTenantProvider>();
        services.AddScoped<ITenantAccessService, TenantAccessService>();
        services.AddScoped<IPermissionResolver, PermissionResolver>();
        services.AddSingleton<IAuthorizationPolicyProvider, OrganizationPermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, OrganizationPermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, OrganizationMembershipAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, ActiveOrganizationPermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, ActiveOrganizationMembershipAuthorizationHandler>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudiences = jwtSettings.Audiences,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrEmpty(context.Token) &&
                            context.Request.Cookies.TryGetValue("access_token", out string? token))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    }
                };
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName, _ => { });

        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddAuthenticationSchemes(
                    JwtBearerDefaults.AuthenticationScheme,
                    ApiKeyAuthenticationHandler.SchemeName)
                .Build();

            options.AddPolicy(
                AuthorizationPolicies.OrganizationMember,
                policy => policy.AddRequirements(new OrganizationMembershipRequirement()));

            options.AddPolicy(
                AuthorizationPolicies.ActiveOrganizationMember,
                policy => policy.AddRequirements(new ActiveOrganizationMembershipRequirement()));
        });

        return services;
    }
}
