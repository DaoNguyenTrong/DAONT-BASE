using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using Respawn;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace StarterKit.API.Tests.TestSupport;

public sealed class ApiFactoryFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("starterkit_api_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly string storageTempDir = Directory.CreateTempSubdirectory("starterkit-api-tests-storage-").FullName;

    private NpgsqlConnection respawnConnection = null!;
    private Respawner respawner = null!;

    public string ConnectionString => container.GetConnectionString();

    /// <summary>Every configuration key the host is pinned to, with its value (<c>Section:Key</c> form).</summary>
    public IReadOnlyDictionary<string, string> PinnedConfiguration { get; private set; } =
        new Dictionary<string, string>();

    // The API project's example settings, copied next to the test assembly by the project reference.
    private static Dictionary<string, string> CommittedExampleConfiguration()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "appsettings.Example.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The API's appsettings.Example.json was not copied to the test output.", path);
        }

        return new ConfigurationBuilder()
            .AddJsonFile(path)
            .Build()
            .AsEnumerable()
            .Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Value!);
    }

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

        // The host loads the developer's gitignored appsettings.json, which differs per machine (lockout limits,
        // cookie flags, a local Redis, real OAuth client ids...), while CI runs with a copy of the example. Pin
        // every key of the committed example first, so the suite tests the same configuration everywhere —
        // TestHostConfigurationTests checks it. Only keys absent from the example still come from the local file.
        Dictionary<string, string> pinned = CommittedExampleConfiguration();

        // Then what the tests themselves need. Isolation pins stay here even when the example already agrees,
        // so a fork editing the example towards real services doesn't send the suite to them: a shared Redis
        // would carry cache entries between runs (Respawn only resets Postgres), a real OAuth ClientId would
        // register the provider ExternalLogin_UnsupportedProvider_ReturnsBadRequest expects to be missing, and
        // FCM credentials would push to real devices.
        pinned["ConnectionStrings:DefaultConnection"] = ConnectionString;
        pinned["RateLimiterSettings:AuthPermitLimit"] = "1000";
        pinned["RateLimiterSettings:AuthWindowMinutes"] = "1";
        pinned["StorageSettings:Provider"] = "Local";
        pinned["StorageSettings:BasePath"] = storageTempDir;
        pinned["CacheSettings:Provider"] = "Memory";
        pinned["RealtimeSettings:Backplane"] = "None";
        pinned["ExternalAuthSettings:Google:ClientId"] = "";
        pinned["ExternalAuthSettings:Microsoft:ClientId"] = "";
        pinned["FcmSettings:ProjectId"] = "";
        pinned["FcmSettings:ServiceAccountJson"] = "";
        PinnedConfiguration = pinned;

        // WebApplicationFactory's ConfigureAppConfiguration doesn't reliably out-prioritize a
        // minimal-API Program.cs's own appsettings.json load, so override via env vars instead —
        // WebApplicationBuilder always reads these last, regardless of Program.cs plumbing.
        foreach ((string key, string value) in pinned)
        {
            Environment.SetEnvironmentVariable(key.Replace(":", "__", StringComparison.Ordinal), value);
        }

        // Force host creation now so the initial (seeded) state is captured before Respawn resets anything.
        _ = Server;

        // AuthTestHelper mints tokens outside the host's DI container — point it at whatever
        // SecretKey the host actually resolved (appsettings.json/env), so minted tokens always
        // validate against the real JwtBearer pipeline regardless of local appsettings.json content.
        using (IServiceScope scope = Services.CreateScope())
        {
            AuthTestHelper.ConfigureJwtSettings(scope.ServiceProvider.GetRequiredService<IOptions<JwtSettings>>().Value);
        }

        respawnConnection = new NpgsqlConnection(ConnectionString);
        await respawnConnection.OpenAsync();
        respawner = await Respawner.CreateAsync(respawnConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            TablesToIgnore = ["__EFMigrationsHistory", "system_settings", "DataProtectionKeys"],
            // Hangfire owns its schema and only (re)creates it at storage startup. Wiping its
            // tables (notably hangfire.schema/lock) makes every extra host built via
            // WithWebHostBuilder re-run the schema install against a half-empty schema and hang.
            SchemasToExclude = ["hangfire"],
            // Respawn's default is one TRUNCATE over every table: ~60ms per reset, since TRUNCATE
            // allocates a new file per table even when it is nearly empty. DELETE on a handful of rows
            // is near-instant, and Respawn still orders child tables before parents. Trade-off: DELETE
            // does NOT reset sequences — never assert a specific auto-increment id ("first session has
            // id 1").
            FormatDeleteStatement = table => $"DELETE FROM {table.GetFullName('"')};"
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPasswordHasher>();
            services.AddSingleton(FastPasswordHasher.Create());

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<SentEmailCollector>();
            services.AddScoped<IEmailSender, CollectingEmailSender>();

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

    /// <summary>What the host has emailed since the last <see cref="ResetAsync"/>.</summary>
    public SentEmailCollector Emails => Services.GetRequiredService<SentEmailCollector>();

    public async Task ResetAsync()
    {
        // A singleton, so without this one test's emails would show up in the next.
        Emails.Clear();
        await respawner.ResetAsync(respawnConnection);
    }

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
