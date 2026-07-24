namespace FeedbackHub.Application.Services.Files;

public sealed record FileDto(
    Guid Id,
    string FileName,
    string ContentType,
    long Size,
    string StoragePath,
    string PublicUrl,
    Guid? OwnerId,
    string? Description,
    string? Category,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
