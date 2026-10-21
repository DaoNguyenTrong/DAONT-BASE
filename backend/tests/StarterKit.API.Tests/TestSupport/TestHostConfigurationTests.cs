using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace StarterKit.API.Tests.TestSupport;

/// <summary>
/// The test host must run the committed <c>appsettings.Example.json</c> plus fixture overrides on every machine, never
/// a developer's gitignored <c>appsettings.json</c>: local settings would silently change what the suite tests (green
/// locally, red on CI, or green only because it reached a real service).
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
