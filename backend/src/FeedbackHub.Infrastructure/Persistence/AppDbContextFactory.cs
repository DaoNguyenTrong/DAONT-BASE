using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FeedbackHub.Infrastructure.Persistence;

/// <summary>
/// Enables <c>dotnet ef</c> without starting the full API host. Reads the same
/// "ConnectionStrings:DefaultConnection" appsettings key as <c>Program.cs</c> — dotnet-ef sets the
/// current directory to the startup project (FeedbackHub.API), so its appsettings.json /
/// appsettings.{ASPNETCORE_ENVIRONMENT}.json are picked up from there.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        string environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("FEEDBACKHUB_DESIGN_TIME_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=FeedbackHub_design;Username=postgres;Password=postgres";

        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
