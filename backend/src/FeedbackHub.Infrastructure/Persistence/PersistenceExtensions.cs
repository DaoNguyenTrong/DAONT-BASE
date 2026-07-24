using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Services.AuditLogs;
using FeedbackHub.Domain.Interfaces;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Persistence.Repositories;
using FeedbackHub.Infrastructure.Services;

namespace FeedbackHub.Infrastructure;

internal static class PersistenceExtensions
{
    internal static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        services.AddDataProtection()
            .PersistKeysToDbContext<AppDbContext>();

        services.AddScoped<ISecretProtector, DataProtectionSecretProtector>();

        services.AddScoped(typeof(IRepository<,>), typeof(GenericRepository<,>));
        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICurrentTenantService, CurrentTenantService>();
        services.AddScoped<IUserTimeZoneProvider, UserTimeZoneProvider>();
        services.AddScoped<ITenantContextResolver, TenantContextResolver>();

        services.AddMemoryCache();
        services.Configure<CacheSettings>(configuration.GetSection(nameof(CacheSettings)));
        services.AddSingleton<ICacheService, MemoryCacheService>();

        services.Configure<RefreshTokenCleanupSettings>(
            configuration.GetSection(nameof(RefreshTokenCleanupSettings)));
        services.AddHostedService<RefreshTokenCleanupService>();

        return services;
    }
}
