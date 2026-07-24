namespace StarterKit.Application.Services.ApiKeys;

public sealed record CreateApiKeyResult(string RawKey, ApiKeyDto Key);
