namespace StarterKit.Application.Services.ApiKeys;

public sealed record ApiKeyDto(
    Guid Id,
    string Name,
    bool IsActive,
    DateTime CreatedAt);
