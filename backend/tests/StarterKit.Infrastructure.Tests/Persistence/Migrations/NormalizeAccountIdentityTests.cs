using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Migrations;
using StarterKit.Infrastructure.Persistence;
using StarterKit.Infrastructure.Tests.TestSupport;

namespace StarterKit.Infrastructure.Tests.Persistence.Migrations;

// Runs the migration's SQL against rows inserted with raw SQL — Account.Create already normalizes,
// so only raw inserts can reproduce the legacy mixed-case data the migration exists for.
[Collection(nameof(PostgresCollection))]
public sealed class NormalizeAccountIdentityTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private AppDbContext context = null!;
    private IDbContextTransaction transaction = null!;

    public async Task InitializeAsync()
    {
        context = fixture.CreateDbContext();
        transaction = await context.Database.BeginTransactionAsync();
    }

    // Never committed — disposing the transaction issues a ROLLBACK, keeping tests isolated.
    public async Task DisposeAsync()
    {
        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }

    private async Task InsertRawAccountAsync(string username, string email)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO accounts ("Id", "Name", "Username", "Email", "CreatedAt")
            VALUES ({Guid.NewGuid()}, 'Legacy', {username}, {email}, now())
            """);
    }

    [Fact]
    public async Task UpSql_MixedCaseRows_AreTrimmedAndLowerCased()
    {
        await InsertRawAccountAsync(" Legacy-User ", "Legacy.User@Example.COM ");

        await context.Database.ExecuteSqlRawAsync(NormalizeAccountIdentity.UpSql);

        Account account = await context.Accounts.AsNoTracking().SingleAsync(a => a.Username == "legacy-user");
        Assert.Equal("legacy.user@example.com", account.Email);
    }

    [Theory]
    [InlineData("dup-user", "a@example.com", "DUP-User", "b@example.com")]
    [InlineData("user-a", "dup@example.com", "user-b", "Dup@Example.com")]
    public async Task UpSql_CaseOnlyDuplicates_AbortsWithoutChangingAnything(
        string username1, string email1, string username2, string email2)
    {
        await InsertRawAccountAsync(username1, email1);
        await InsertRawAccountAsync(username2, email2);

        PostgresException error = await Assert.ThrowsAsync<PostgresException>(
            () => context.Database.ExecuteSqlRawAsync(NormalizeAccountIdentity.UpSql));

        Assert.Contains("NormalizeAccountIdentity", error.MessageText);
        // Counts only — no email address leaks into the startup log.
        Assert.DoesNotContain("@", error.MessageText);
    }

    // lower() follows the database's ctype: under a C-locale database it leaves non-ASCII letters alone,
    // while every lookup normalizes with .NET ToLowerInvariant — the row would never match its login.
    [Fact]
    public async Task UpSql_OnCLocaleDatabase_LowerCasesNonAsciiLikeTheApplication()
    {
        string database = $"c_locale_{Guid.NewGuid():N}";
        await using (NpgsqlConnection admin = new(fixture.ConnectionString))
        {
            await admin.OpenAsync();
            await using NpgsqlCommand create = new($"CREATE DATABASE {database} TEMPLATE template0 LOCALE 'C'", admin);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            NpgsqlConnectionStringBuilder builder = new(fixture.ConnectionString) { Database = database, Pooling = false };
            await using NpgsqlConnection connection = new(builder.ConnectionString);
            await connection.OpenAsync();
            await using (NpgsqlCommand setup = new("""
                CREATE TABLE accounts ("Username" text NOT NULL, "Email" text NOT NULL);
                INSERT INTO accounts VALUES ('ÉLISE', 'Élise@Example.com');
                """, connection))
            {
                await setup.ExecuteNonQueryAsync();
            }

            await using (NpgsqlCommand migrate = new(NormalizeAccountIdentity.UpSql, connection))
            {
                await migrate.ExecuteNonQueryAsync();
            }

            await using NpgsqlCommand read = new("""SELECT "Username", "Email" FROM accounts""", connection);
            await using NpgsqlDataReader reader = await read.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(Account.NormalizeUsername("ÉLISE"), reader.GetString(0));
            Assert.Equal(Account.NormalizeEmail("Élise@Example.com"), reader.GetString(1));
        }
        finally
        {
            await using NpgsqlConnection admin = new(fixture.ConnectionString);
            await admin.OpenAsync();
            await using NpgsqlCommand drop = new($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
