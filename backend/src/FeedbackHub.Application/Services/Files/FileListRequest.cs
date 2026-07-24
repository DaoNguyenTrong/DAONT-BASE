namespace FeedbackHub.Application.Services.Files;

public sealed record FileListRequest(int PageNumber = 1, int PageSize = 10);
