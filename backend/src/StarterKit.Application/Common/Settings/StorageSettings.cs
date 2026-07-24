namespace StarterKit.Application.Common.Settings;

public sealed class StorageSettings
{
    public string Provider { get; init; } = "Local";

    public string BasePath { get; init; } = "uploads";

    public string PublicUrlBase { get; init; } = "/storage";

    public long MaxFileSizeBytes { get; init; } = 10_485_760;

    public string[] AllowedContentTypes { get; init; } = [];
}
