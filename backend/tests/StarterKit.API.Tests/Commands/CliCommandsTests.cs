using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StarterKit.API.Commands;
using StarterKit.Application.Common.Interfaces;

namespace StarterKit.API.Tests.Commands;

public sealed class CliCommandsTests
{
    private static (IServiceProvider Services, IDatabaseMigrator Migrator) CreateServices()
    {
        IDatabaseMigrator migrator = Substitute.For<IDatabaseMigrator>();
        ServiceCollection services = new();
        services.AddScoped(_ => migrator);
        return (services.BuildServiceProvider(), migrator);
    }

    // Not a command → null, and Program.cs goes on to start the web host. Host switches such as --urls are
    // the common first argument, so they must never be taken for a verb.
    [Theory]
    [InlineData]
    [InlineData("--urls", "http://localhost:5000")]
    [InlineData("not-a-command")]
    public async Task TryRunAsync_NotACommand_ReturnsNullAndRunsNothing(params string[] args)
    {
        (IServiceProvider services, IDatabaseMigrator migrator) = CreateServices();

        int? exitCode = await CliCommands.TryRunAsync(services, args, CancellationToken.None);

        Assert.Null(exitCode);
        await migrator.DidNotReceiveWithAnyArgs().MigrateAsync(default);
    }

    [Theory]
    [InlineData("migrate")]
    [InlineData("MIGRATE")]
    public async Task TryRunAsync_Migrate_AppliesMigrationsAndExitsZero(string verb)
    {
        (IServiceProvider services, IDatabaseMigrator migrator) = CreateServices();

        int? exitCode = await CliCommands.TryRunAsync(services, [verb], CancellationToken.None);

        Assert.Equal(0, exitCode);
        await migrator.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
    }

    // A typo in a deploy script must fail loudly, not silently run the command with the extra argument dropped.
    [Fact]
    public async Task TryRunAsync_MigrateWithUnexpectedArgument_PrintsUsageAndExitsOne()
    {
        (IServiceProvider services, IDatabaseMigrator migrator) = CreateServices();

        int? exitCode = await CliCommands.TryRunAsync(services, ["migrate", "--force"], CancellationToken.None);

        Assert.Equal(1, exitCode);
        await migrator.DidNotReceiveWithAnyArgs().MigrateAsync(default);
    }

    private sealed class AsyncOnlyDisposableMigrator : IDatabaseMigrator, IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public Task MigrateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    // EF's DbContext and many clients dispose asynchronously; a sync scope Dispose() throws on a service that
    // implements only IAsyncDisposable, turning a successful command into a crash.
    [Fact]
    public async Task TryRunAsync_ScopedServiceDisposesOnlyAsynchronously_IsDisposedAndCommandSucceeds()
    {
        AsyncOnlyDisposableMigrator migrator = new();
        ServiceCollection collection = new();
        collection.AddScoped<IDatabaseMigrator>(_ => migrator);
        await using ServiceProvider services = collection.BuildServiceProvider();

        int? exitCode = await CliCommands.TryRunAsync(services, ["migrate"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.True(migrator.Disposed);
    }

    [Fact]
    public void All_VerbsAreUniqueIgnoringCase()
    {
        string[] verbs = [.. CliCommands.All.Select(command => command.Verb.ToLowerInvariant())];

        Assert.Equal(verbs.Length, verbs.Distinct().Count());
    }
}
