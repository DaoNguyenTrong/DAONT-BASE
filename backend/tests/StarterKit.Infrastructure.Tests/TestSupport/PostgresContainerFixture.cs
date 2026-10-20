using Microsoft.EntityFrameworkCore;
using Npgsql;
using StarterKit.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace StarterKit.Infrastructure.Tests.TestSupport;

/// <summary>
/// One Postgres container per test process, migrated once into a template; each collection clones its own database
/// from it (<c>CREATE DATABASE … TEMPLATE</c>, ~100ms vs a migration run), so collections run in parallel in isolation.
/// Testcontainers' Ryuk removes the container, and the clones with it, when the process exits.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private const string TemplateDatabase = "starterkit_infra_template";

    private static readonly Lazy<Task<PostgreSqlContainer>> SharedContainer = new(StartTemplateAsync);

    private string connectionString = null!;

    /// <summary>This collection's own database.</summary>
    public string ConnectionString => connectionString;

    public async Task InitializeAsync()
    {
        PostgreSqlContainer container = await SharedContainer.Value;
        string database = $"starterkit_infra_{Guid.NewGuid():N}";

        await using (NpgsqlConnection admin = new(AdminConnectionString(container)))
        {
            await admin.OpenAsync();
            await using NpgsqlCommand create = new(
                $"CREATE DATABASE \"{database}\" TEMPLATE \"{TemplateDatabase}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        connectionString = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = database
        }.ConnectionString;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public AppDbContext CreateDbContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<PostgreSqlContainer> StartTemplateAsync()
    {
        PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase(TemplateDatabase)
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await container.StartAsync();

        // CREATE DATABASE … TEMPLATE fails (55006 "being accessed by other users") while any session is
        // connected to the template, so the migration doesn't go through the pool: disposing the context
        // really closes its connection, instead of leaving an idle one parked on the template.
        string migrationConnection = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Pooling = false
        }.ConnectionString;
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(migrationConnection)
            .Options;
        await using (AppDbContext context = new(options))
        {
            await context.Database.MigrateAsync();
        }

        return container;
    }

    private static string AdminConnectionString(PostgreSqlContainer container) =>
        new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = "postgres" }.ConnectionString;
}

// Collections run in parallel, each on its own clone; classes inside one run one after another. A class that
// commits rows another class reads, or changes the schema, stays in PostgresCollection with the others that
// commit; classes that roll back every test go in PostgresCollectionB. Add a collection when one grows slow.
[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>;

[CollectionDefinition(nameof(PostgresCollectionB))]
public sealed class PostgresCollectionB : ICollectionFixture<PostgresContainerFixture>;
