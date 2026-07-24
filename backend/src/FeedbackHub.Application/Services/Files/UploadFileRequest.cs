namespace FeedbackHub.Application.Services.Files;

public sealed record UploadFileRequest(
    Stream Content,
    string FileName,
    string ContentType,
    long Size,
    Guid? OwnerId = null,
    string? Description = null,
    string? Category = null);
