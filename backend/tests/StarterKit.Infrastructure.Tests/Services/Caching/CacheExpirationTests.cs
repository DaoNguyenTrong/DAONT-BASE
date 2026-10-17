using StarterKit.Application.Common.Settings;
using StarterKit.Infrastructure.Services.Caching;

namespace StarterKit.Infrastructure.Tests.Services.Caching;

public class CacheExpirationTests
{
    private static readonly CacheSettings Settings = new() { DefaultExpirationMinutes = 5, MaxExpirationMinutes = 60 };

    [Fact]
    public void Resolve_NoExpiration_UsesTheDefault() =>
        Assert.Equal(TimeSpan.FromMinutes(5), CacheExpiration.Resolve(Settings, null));

    [Fact]
    public void Resolve_WithinTheMaximum_IsKept() =>
        Assert.Equal(TimeSpan.FromSeconds(90), CacheExpiration.Resolve(Settings, TimeSpan.FromSeconds(90)));

    // Clamped, not rejected: PermissionResolver/TenantAccessService pass a configured TTL on every
    // authenticated request, and a cache must not fail those requests over it.
    [Fact]
    public void Resolve_AboveTheMaximum_IsClamped() =>
        Assert.Equal(TimeSpan.FromMinutes(60), CacheExpiration.Resolve(Settings, TimeSpan.FromDays(1)));

    [Fact]
    public void GenerationTtl_OutlivesTheLongestEntry() =>
        Assert.True(CacheExpiration.GenerationTtl(Settings) > TimeSpan.FromMinutes(Settings.MaxExpirationMinutes));

    [Theory]
    [InlineData(5, 0)]
    [InlineData(0, 60)]
    [InlineData(-1, 60)]
    public void Validate_RejectsANonPositiveExpiration(int defaultMinutes, int maxMinutes)
    {
        CacheSettings settings = new() { DefaultExpirationMinutes = defaultMinutes, MaxExpirationMinutes = maxMinutes };

        Assert.Throws<InvalidOperationException>(() => CacheExpiration.Validate(settings));
    }

    [Fact]
    public void Validate_AcceptsTheDefaults() => CacheExpiration.Validate(new CacheSettings());

    // Clamped like any other TTL, so a long default in an existing configuration still starts.
    [Fact]
    public void DefaultAboveTheMaximum_IsAcceptedAndClamped()
    {
        CacheSettings settings = new() { DefaultExpirationMinutes = 2880, MaxExpirationMinutes = 1440 };

        CacheExpiration.Validate(settings);
        Assert.Equal(TimeSpan.FromMinutes(1440), CacheExpiration.Resolve(settings, null));
    }
}
