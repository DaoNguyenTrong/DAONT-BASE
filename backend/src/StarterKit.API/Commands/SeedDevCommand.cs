using StarterKit.Application.Services.DevData;

namespace StarterKit.API.Commands;

/// <summary>
/// Creates the fixed local accounts the Playwright suite logs in with (<c>frontend/e2e</c>), plus an organization
/// they belong to. Idempotent. Refuses to run in Production: the credentials below are public in this repository.
/// </summary>
public sealed class SeedDevCommand : ICliCommand
{
    public const string OwnerUsername = "e2e-admin";
    public const string MemberUsername = "e2e-user";

    public string Verb => "seed-dev";

    public string Usage => string.Empty;

    public async Task<int> RunAsync(IServiceProvider services, string[] args, CancellationToken cancellationToken)
    {
        if (args.Length != 0)
        {
            return 1;
        }

        if (services.GetRequiredService<IHostEnvironment>().IsProduction())
        {
            Console.Error.WriteLine("seed-dev refuses to run in Production: its credentials are public.");
            return 2;
        }

        DevSeedRequest request = new(
            OrganizationName: "Dev",
            OrganizationSlug: "dev",
            Owner: new DevSeedAccount(OwnerUsername, "e2e-admin@test.local", "E2E Admin", "E2e-Admin!2026"),
            Member: new DevSeedAccount(MemberUsername, "e2e-user@test.local", "E2E User", "E2e-User!2026"));

        await services.GetRequiredService<IDevDataSeeder>().SeedAsync(request, cancellationToken);
        Console.WriteLine($"Dev data is seeded: {OwnerUsername} (owner), {MemberUsername} (member).");
        return 0;
    }
}
