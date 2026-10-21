namespace StarterKit.Application.Common.Settings;

public sealed class MinioSettings
{
    public string ServiceUrl { get; init; } = "http://localhost:9000";

    public string AccessKey { get; init; } = "";

    public string SecretKey { get; init; } = "";

    public string BucketName { get; init; } = "starterkit";
}
