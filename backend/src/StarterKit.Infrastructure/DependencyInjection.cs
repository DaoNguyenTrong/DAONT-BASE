using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace StarterKit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddSecurity();
        services.AddContext();
        services.AddCaching(configuration);
        services.AddJwtAuthentication(configuration);
        services.AddStorage(configuration);
        services.AddEmail(configuration);
        services.AddExternalAuth(configuration);
        return services;
    }
}
