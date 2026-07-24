using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Respawn;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace StarterKit.API.Tests.TestSupport;

public sealed class ApiFactoryFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("starterkit_api_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly string storageTempDir = Directory.CreateTempSubdirectory("starterkit-api-tests-storage-").FullName;

    private NpgsqlConnection respawnConnection = null!;
    private Respawner respawner = null!;

    public string ConnectionString => container.GetConnectionString();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await container.StartAsync();

        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using (AppDbContext migrateContext = new(options))
        {
            await migrateContext.Database.MigrateAsync();
        }

        // WebApplicationFactory's ConfigureAppConfiguration doesn't reliably out-prioritize a
        // minimal-API Program.cs's own appsettings.json load, so override via env vars instead —
        // WebApplicationBuilder always reads these last, regardless of Program.cs plumbing.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", ConnectionString);
        Environment.SetEnvironmentVariable("RateLimiterSettings__AuthPermitLimit", "1000");
        Environment.SetEnvironmentVariable("RateLimiterSettings__AuthWindowMinutes", "1");
        Environment.SetEnvironmentVariable("StorageSettings__BasePath", storageTempDir);

        // Force host creation now so the initial (seeded) state is captured before Respawn resets anything.
        _ = Server;

        respawnConnection = new NpgsqlConnection(ConnectionString);
        await respawnConnection.OpenAsync();
        respawner = await Respawner.CreateAsync(respawnConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            TablesToIgnore = ["__EFMigrationsHistory", "system_settings", "DataProtectionKeys"]
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddScoped<IEmailSender, NoOpEmailSender>();

            // Avoid a background hosted service resolving a scoped AppDbContext during host teardown.
            services.RemoveAll<IHostedService>();
        });
    }

    public HttpClient CreateTestClient()
    {
        HttpClient client = CreateClient();
        client.DefaultRequestHeaders.Add("X-TimeZone", "UTC");
        return client;
    }

    public async Task ResetAsync() => await respawner.ResetAsync(respawnConnection);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await respawnConnection.DisposeAsync();
        await container.DisposeAsync();
        if (Directory.Exists(storageTempDir))
        {
            Directory.Delete(storageTempDir, recursive: true);
        }
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFactoryFixture>;
