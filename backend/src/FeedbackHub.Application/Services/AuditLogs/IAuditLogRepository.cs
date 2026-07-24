namespace FeedbackHub.Application.Services.AuditLogs;

public interface IAuditLogRepository
{
    Task<(IReadOnlyList<AuditLogDto> Items, int TotalCount)> ListPagedAsync(
        int pageNumber,
        int pageSize,
        string? search,
        Guid? userId,
        bool? systemOnly,
        CancellationToken cancellationToken = default);

    Task<AuditLogDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
}
