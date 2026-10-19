namespace StarterKit.API.Commands;

public static class CliCommands
{
    public static readonly IReadOnlyList<ICliCommand> All =
    [
        new MigrateCommand(),
        new SeedDevCommand()
    ];

    /// <summary>
    /// Runs the command named by <paramref name="args"/>[0] and returns its exit code, or <c>null</c> when the
    /// arguments don't name one — the caller then starts the web host as usual.
    /// </summary>
    public static async Task<int?> TryRunAsync(
        IServiceProvider services,
        string[] args,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            return null;
        }

        ICliCommand? command = All.FirstOrDefault(
            candidate => string.Equals(candidate.Verb, args[0], StringComparison.OrdinalIgnoreCase));

        if (command is null)
        {
            return null;
        }

        // Async: scoped services such as DbContext may dispose only asynchronously.
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        int exitCode = await command.RunAsync(scope.ServiceProvider, args[1..], cancellationToken);

        if (exitCode == 1)
        {
            Console.Error.WriteLine(
                $"Usage: dotnet run --project backend/src/StarterKit.API -- {command.Verb} {command.Usage}".TrimEnd());
        }

        return exitCode;
    }
}
