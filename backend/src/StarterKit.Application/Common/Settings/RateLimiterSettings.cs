namespace StarterKit.Application.Common.Settings;

public sealed class RateLimiterSettings
{
    public int AuthPermitLimit { get; init; } = 5;

    public int AuthWindowMinutes { get; init; } = 1;
}
