namespace StarterKit.Application.Common.Settings;

public sealed class SeaweedFsSettings
{
    public string ServiceUrl { get; init; } = "http://localhost:8333";

    public string AccessKey { get; init; } = "";

    public string SecretKey { get; init; } = "";

    public string BucketName { get; init; } = "starterkit";
}
