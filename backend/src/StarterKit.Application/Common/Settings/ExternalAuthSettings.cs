namespace StarterKit.Application.Common.Settings;

public sealed class ExternalAuthSettings
{
    public GoogleAuthSettings Google { get; init; } = new();
}

public sealed class GoogleAuthSettings
{
    public string ClientId { get; init; } = string.Empty;
}
