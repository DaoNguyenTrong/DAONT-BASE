using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace StarterKit.API.Tests.TestSupport;

/// <summary>
/// Guards the test host's configuration: it must be the committed <c>appsettings.Example.json</c> plus the
/// fixture's own overrides on every machine, never what a developer's gitignored <c>appsettings.json</c> holds.
/// Without this a local lockout limit, cookie flag or Redis provider silently changes what the suite tests —
/// green on one machine, red on CI, or worse, green because it reached a real service.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class TestHostConfigurationTests(ApiFactoryFixture fixture)
{
    [Fact]
    public void Host_ResolvesEveryPinnedKeyToItsPinnedValue()
    {
        IConfiguration configuration = fixture.Services.GetRequiredService<IConfiguration>();

        string[] mismatched = [.. fixture.PinnedConfiguration
            .Where(pair => configuration[pair.Key] != pair.Value)
            .Select(pair => pair.Key)];

        Assert.NotEmpty(fixture.PinnedConfiguration);
        Assert.Empty(mismatched);
    }

    // The isolation overrides hold even if a fork edits the example file towards real services.
    [Theory]
    [InlineData("CacheSettings:Provider", "Memory")]
    [InlineData("RealtimeSettings:Backplane", "None")]
    [InlineData("StorageSettings:Provider", "Local")]
    [InlineData("FcmSettings:ServiceAccountJson", "")]
    [InlineData("ExternalAuthSettings:Google:ClientId", "")]
    [InlineData("ExternalAuthSettings:Microsoft:ClientId", "")]
    public void Host_PinsServicesThatWouldReachOutsideTheTestProcess(string key, string expected)
    {
        IConfiguration configuration = fixture.Services.GetRequiredService<IConfiguration>();

        Assert.Equal(expected, configuration[key] ?? "");
    }
}
