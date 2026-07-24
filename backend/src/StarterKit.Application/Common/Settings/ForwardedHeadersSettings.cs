namespace StarterKit.Application.Common.Settings;

public sealed class ForwardedHeadersSettings
{
    public string[] KnownProxies { get; init; } = [];

    public string[] KnownNetworks { get; init; } = [];
}
