namespace StarterKit.Application.Services.Files;

public sealed record FileDto(
    Guid Id,
    string FileName,
    string ContentType,
    long Size,
    string StoragePath,
    string PublicUrl,
    Guid OrganizationId,
    string? Description,
    string? Category,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
