using StarterKit.Application.Common.Models;

namespace StarterKit.Application.Services.AuditLogs;

public interface IAuditLogService
{
    Task<PagedResult<AuditLogDto>> GetAllAsync(
        PaginationRequest request,
        Guid? userId,
        bool? systemOnly,
        CancellationToken cancellationToken = default);

    Task<AuditLogDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);
}
