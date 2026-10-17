using StarterKit.Application.Common.Interfaces;

namespace StarterKit.API.Commands;

/// <summary>
/// Applies pending migrations and exits — the deploy step for running several API instances: set
/// <c>DatabaseSettings:MigrateOnStartup=false</c> on the instances (otherwise each migrates on startup and they
/// race on the same migration), run this once, then start them.
/// </summary>
public sealed class MigrateCommand : ICliCommand
{
    public string Verb => "migrate";

    public string Usage => string.Empty;

    public async Task<int> RunAsync(IServiceProvider services, string[] args, CancellationToken cancellationToken)
    {
        if (args.Length != 0)
        {
            return 1;
        }

        await services.GetRequiredService<IDatabaseMigrator>().MigrateAsync(cancellationToken);
        Console.WriteLine("Database is up to date.");
        return 0;
    }
}
