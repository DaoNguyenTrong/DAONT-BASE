using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using StarterKit.API.Commands;
using StarterKit.Application.Services.DevData;

namespace StarterKit.API.Tests.Commands;

public sealed class SeedDevCommandTests
{
    private static (IServiceProvider Services, IDevDataSeeder Seeder) CreateServices(string environmentName)
    {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        IDevDataSeeder seeder = Substitute.For<IDevDataSeeder>();

        ServiceCollection services = new();
        services.AddSingleton(environment);
        services.AddScoped(_ => seeder);
        return (services.BuildServiceProvider(), seeder);
    }

    [Fact]
    public async Task TryRunAsync_SeedDevInDevelopment_SeedsAndExitsZero()
    {
        (IServiceProvider services, IDevDataSeeder seeder) = CreateServices(Environments.Development);

        int? exitCode = await CliCommands.TryRunAsync(services, ["seed-dev"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        await seeder.Received(1).SeedAsync(
            Arg.Is<DevSeedRequest>(request =>
                request.Owner.Username == SeedDevCommand.OwnerUsername
                && request.Member.Username == SeedDevCommand.MemberUsername),
            Arg.Any<CancellationToken>());
    }

    // The seed's credentials are committed to this repository, so a Production run must never create them.
    [Fact]
    public async Task TryRunAsync_SeedDevInProduction_RefusesWithoutSeeding()
    {
        (IServiceProvider services, IDevDataSeeder seeder) = CreateServices(Environments.Production);

        int? exitCode = await CliCommands.TryRunAsync(services, ["seed-dev"], CancellationToken.None);

        Assert.Equal(2, exitCode);
        await seeder.DidNotReceive().SeedAsync(Arg.Any<DevSeedRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryRunAsync_SeedDevWithUnexpectedArgument_PrintsUsageAndExitsOne()
    {
        (IServiceProvider services, IDevDataSeeder seeder) = CreateServices(Environments.Development);

        int? exitCode = await CliCommands.TryRunAsync(services, ["seed-dev", "--force"], CancellationToken.None);

        Assert.Equal(1, exitCode);
        await seeder.DidNotReceive().SeedAsync(Arg.Any<DevSeedRequest>(), Arg.Any<CancellationToken>());
    }
}
