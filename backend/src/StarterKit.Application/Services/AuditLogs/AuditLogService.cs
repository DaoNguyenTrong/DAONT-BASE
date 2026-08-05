using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Models;
using StarterKit.Application.Resources;
using StarterKit.Domain.Exceptions;

namespace StarterKit.Application.Services.AuditLogs;

public sealed class AuditLogService(
    IAuditLogRepository auditLogRepository,
    ICurrentTenantProvider currentTenantProvider) : IAuditLogService
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 10;

    public async Task<PagedResult<AuditLogDto>> GetAllAsync(
        PaginationRequest request,
        Guid? userId,
        bool? systemOnly,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = RequireOrganizationId();
        int pageNumber = request.PageNumber < 1 ? DefaultPageNumber : request.PageNumber;
        int pageSize = request.PageSize < 1 ? DefaultPageSize : request.PageSize;
        string? search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();

        (IReadOnlyList<AuditLogDto> items, int totalCount) = await auditLogRepository.ListPagedAsync(
            organizationId,
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
        Guid organizationId = RequireOrganizationId();

        return await auditLogRepository.GetByIdAsync(organizationId, id, cancellationToken)
            ?? throw new NotFoundException("AuditLog", id);
    }

    private Guid RequireOrganizationId() =>
        currentTenantProvider.OrganizationId
            ?? throw new ForbiddenException(ApplicationMessages.OrganizationAccessDenied);
}
