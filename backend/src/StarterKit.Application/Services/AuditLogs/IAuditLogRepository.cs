namespace StarterKit.Application.Services.AuditLogs;

public interface IAuditLogRepository
{
    Task<(IReadOnlyList<AuditLogDto> Items, int TotalCount)> ListPagedAsync(
        Guid organizationId,
        int pageNumber,
        int pageSize,
        string? search,
        Guid? userId,
        bool? systemOnly,
        CancellationToken cancellationToken = default);

    Task<AuditLogDto?> GetByIdAsync(Guid organizationId, long id, CancellationToken cancellationToken = default);
}
