using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StarterKit.Application.Common.Authorization;
using StarterKit.Application.Common.Models;
using StarterKit.Application.Services.AuditLogs;

namespace StarterKit.API.Controllers;

/// <summary>Audit trail for the caller's active organization (JWT <c>org_id</c> claim).</summary>
[ApiController]
[Authorize(Policy = Permissions.AuditLogsView)]
[Route("api/admin/audit-logs")]
public sealed class AuditLogsController(IAuditLogService auditLogService) : ControllerBase
{
    /// <summary>Returns a paginated list of audit logs.</summary>
    /// <param name="request">Pagination and free-text search.</param>
    /// <param name="userId">Filter to audit logs performed by this account.</param>
    /// <param name="systemOnly">When true, filter to system-generated audit logs (no associated account).</param>
    /// <param name="cancellationToken"></param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> GetAll(
        [FromQuery] PaginationRequest request,
        [FromQuery] Guid? userId,
        [FromQuery] bool? systemOnly,
        CancellationToken cancellationToken)
    {
        PagedResult<AuditLogDto> auditLogs = await auditLogService.GetAllAsync(request, userId, systemOnly, cancellationToken);

        return Ok(auditLogs);
    }

    /// <summary>Returns the details of an audit log by ID.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(AuditLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditLogDto>> GetById(long id, CancellationToken cancellationToken)
    {
        AuditLogDto auditLog = await auditLogService.GetByIdAsync(id, cancellationToken);

        return Ok(auditLog);
    }
}
