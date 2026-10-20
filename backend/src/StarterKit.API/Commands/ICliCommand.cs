namespace StarterKit.API.Commands;

/// <summary>
/// A one-shot command run through the API host (config + DI) instead of the web server, for work that must succeed
/// when HTTP can't, from a script branching on the exit code. Register in <see cref="CliCommands.All"/>. Run with
/// <c>dotnet run --project backend/src/StarterKit.API -- &lt;verb&gt;</c> (image: <c>dotnet StarterKit.API.dll</c>).
/// </summary>
public interface ICliCommand
{
    /// <summary>First command-line argument that selects this command, matched ignoring case.</summary>
    string Verb { get; }

    /// <summary>Arguments after the verb, as shown in the usage line.</summary>
    string Usage { get; }

    /// <summary>
    /// Runs with <paramref name="services"/> from a scope of its own; <paramref name="args"/> excludes the verb.
    /// Returns the process exit code: 0 on success, 1 for bad usage, anything else the command documents.
    /// </summary>
    Task<int> RunAsync(IServiceProvider services, string[] args, CancellationToken cancellationToken);
}
