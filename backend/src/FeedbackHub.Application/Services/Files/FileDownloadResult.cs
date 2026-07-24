namespace FeedbackHub.Application.Services.Files;

public sealed record FileDownloadResult(
    Stream Content,
    string FileName,
    string ContentType);
