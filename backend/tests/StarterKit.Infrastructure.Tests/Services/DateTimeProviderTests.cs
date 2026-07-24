using StarterKit.Infrastructure.Services;

namespace StarterKit.Infrastructure.Tests.Services;

public class DateTimeProviderTests
{
    [Fact]
    public void UtcNow_ReturnsCurrentUtcTime()
    {
        DateTimeProvider provider = new();

        DateTime before = DateTime.UtcNow;
        DateTime value = provider.UtcNow;
        DateTime after = DateTime.UtcNow;

        Assert.Equal(DateTimeKind.Utc, value.Kind);
        Assert.InRange(value, before.AddSeconds(-1), after.AddSeconds(1));
    }
}
