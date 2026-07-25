namespace StarterKit.Application.Common.Settings;

public sealed class ExternalAuthSettings
{
    public GoogleAuthSettings Google { get; init; } = new();
    public MicrosoftAuthSettings Microsoft { get; init; } = new();
}

public sealed class GoogleAuthSettings
{
    public string ClientId { get; init; } = string.Empty;
}

public sealed class MicrosoftAuthSettings
{
    public string ClientId { get; init; } = string.Empty;
    public string TenantId { get; init; } = "common";
}
