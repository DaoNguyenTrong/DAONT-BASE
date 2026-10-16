namespace StarterKit.API.Commands;

/// <summary>
/// A one-shot command run through the API's own host instead of starting the web server:
/// <code>dotnet run --project backend/src/StarterKit.API -- &lt;verb&gt; [args]</code>
/// (in the production image: <c>dotnet StarterKit.API.dll &lt;verb&gt; [args]</c>).
/// It gets the same configuration and DI container as the API, so it calls Application services like a
/// controller would — for seeding, imports, granting a role, or anything that must work when the HTTP
/// side can't, from a script that branches on the exit code. Register new commands in <see cref="CliCommands.All"/>.
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
