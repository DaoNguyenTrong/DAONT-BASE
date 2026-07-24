using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FeedbackHub.Application.Services.Tenants;

namespace FeedbackHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class TenantsController(ITenantService tenantService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TenantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<TenantDto>>> GetMyTenants(CancellationToken cancellationToken)
        => Ok(await tenantService.GetMyTenantsAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await tenantService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TenantDto>> Create(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        TenantDto tenant = await tenantService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
    }

    [HttpPost("{id:guid}/members")]
    [ProducesResponseType(typeof(TenantMembershipDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TenantMembershipDto>> InviteMember(
        Guid id,
        InviteMemberRequest request,
        CancellationToken cancellationToken)
    {
        TenantMembershipDto membership = await tenantService.InviteAsync(id, request.AccountId, cancellationToken);
        return CreatedAtAction(nameof(ListMembers), new { id }, membership);
    }

    [HttpDelete("{id:guid}/members/{accountId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveMember(
        Guid id,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await tenantService.RemoveAsync(id, accountId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/transfer-owner")]
    [ProducesResponseType(typeof(TenantMembershipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TenantMembershipDto>> TransferOwnership(
        Guid id,
        TransferOwnershipRequest request,
        CancellationToken cancellationToken)
    {
        TenantMembershipDto newOwner = await tenantService.TransferOwnershipAsync(id, request.NewOwnerId, cancellationToken);
        return Ok(newOwner);
    }

    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<TenantMembershipDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TenantMembershipDto>>> ListMembers(
        Guid id,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TenantMembershipDto> members = await tenantService.ListMembersAsync(id, cancellationToken);
        return Ok(members);
    }
}
