using FeedbackHub.Application.Common.Models;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Application.Services.AuditLogs;

public sealed class AuditLogService(IAuditLogRepository auditLogRepository) : IAuditLogService
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 10;

    public async Task<PagedResult<AuditLogDto>> GetAllAsync(
        PaginationRequest request,
        Guid? userId,
        bool? systemOnly,
        CancellationToken cancellationToken = default)
    {
        int pageNumber = request.PageNumber < 1 ? DefaultPageNumber : request.PageNumber;
        int pageSize = request.PageSize < 1 ? DefaultPageSize : request.PageSize;
        string? search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();

        (IReadOnlyList<AuditLogDto> items, int totalCount) = await auditLogRepository.ListPagedAsync(
            pageNumber,
            pageSize,
            search,
            userId,
            systemOnly,
            cancellationToken);

        return new PagedResult<AuditLogDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<AuditLogDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await auditLogRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("AuditLog", id);
    }
}
