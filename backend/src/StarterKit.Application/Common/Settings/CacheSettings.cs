namespace StarterKit.Application.Common.Settings;

public sealed class CacheSettings
{
    public string Provider { get; init; } = "Memory";

    public int DefaultExpirationMinutes { get; init; } = 5;
}
