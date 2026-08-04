using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StarterKit.Application.Common.Authorization;
using StarterKit.Application.Services.Organizations;
using StarterKit.Application.Services.Roles;
using StarterKit.Infrastructure.Services.Auth;

namespace StarterKit.API.Controllers;

[ApiController]
[Authorize]
[Route("api/organizations")]
public sealed class OrganizationsController(
    IOrganizationService organizationService,
    IOrganizationMembershipService organizationMembershipService,
    IRoleService roleService) : ControllerBase
{
    /// <summary>Creates a new organization. The caller becomes its Owner.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OrganizationDto>> Create(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        OrganizationDto organization = await organizationService.CreateAsync(request, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, organization);
    }

    /// <summary>Returns the organizations the current account is an active member of.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OrganizationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<OrganizationDto>>> GetMine(CancellationToken cancellationToken)
    {
        IReadOnlyList<OrganizationDto> organizations = await organizationService.ListMineAsync(cancellationToken);

        return Ok(organizations);
    }

    /// <summary>Returns the members of an organization. The caller must be an active member.</summary>
    [HttpGet("{id:guid}/members")]
    [Authorize(Policy = AuthorizationPolicies.OrganizationMember)]
    [ProducesResponseType(typeof(IReadOnlyList<OrganizationMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<OrganizationMemberDto>>> GetMembers(
        Guid id,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<OrganizationMemberDto> members = await organizationService.GetMembersAsync(id, cancellationToken);

        return Ok(members);
    }

    /// <summary>Adds an existing account as a member of the organization. The caller must be an Owner or Admin.</summary>
    [HttpPost("{id:guid}/members")]
    [Authorize(Policy = Permissions.OrganizationMembersManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddMember(
        Guid id,
        AddMemberRequest request,
        CancellationToken cancellationToken)
    {
        await organizationMembershipService.AddMemberAsync(id, request, cancellationToken);

        return NoContent();
    }

    /// <summary>Changes a member's roles. The caller must hold the members-manage permission.</summary>
    [HttpPatch("{id:guid}/members/{accountId:guid}")]
    [Authorize(Policy = Permissions.OrganizationMembersManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateMemberRoles(
        Guid id,
        Guid accountId,
        UpdateMemberRolesRequest request,
        CancellationToken cancellationToken)
    {
        await organizationMembershipService.UpdateMemberRolesAsync(id, accountId, request, cancellationToken);

        return NoContent();
    }

    /// <summary>Removes a member from the organization. The caller must be an Owner or Admin.</summary>
    [HttpDelete("{id:guid}/members/{accountId:guid}")]
    [Authorize(Policy = Permissions.OrganizationMembersManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveMember(
        Guid id,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await organizationMembershipService.RemoveMemberAsync(id, accountId, cancellationToken);

        return NoContent();
    }


    /// <summary>Deactivates an organization. The caller must be its Owner; all members immediately lose access.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.OrganizationManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await organizationService.DeactivateAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>Returns the roles defined for an organization. The caller must be an active member.</summary>
    [HttpGet("{id:guid}/roles")]
    [Authorize(Policy = AuthorizationPolicies.OrganizationMember)]
    [ProducesResponseType(typeof(IReadOnlyList<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetRoles(Guid id, CancellationToken cancellationToken)
    {
        IReadOnlyList<RoleDto> roles = await roleService.ListAsync(id, cancellationToken);

        return Ok(roles);
    }

    /// <summary>Creates a custom role for an organization. The caller must hold the roles-manage permission.</summary>
    [HttpPost("{id:guid}/roles")]
    [Authorize(Policy = Permissions.OrganizationRolesManage)]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoleDto>> CreateRole(
        Guid id, CreateRoleRequest request, CancellationToken cancellationToken)
    {
        RoleDto role = await roleService.CreateAsync(id, request, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, role);
    }

    /// <summary>Updates a custom role's name/permissions. System roles cannot be edited.</summary>
    [HttpPatch("{id:guid}/roles/{roleId:guid}")]
    [Authorize(Policy = Permissions.OrganizationRolesManage)]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoleDto>> UpdateRole(
        Guid id, Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        RoleDto role = await roleService.UpdateAsync(id, roleId, request, cancellationToken);

        return Ok(role);
    }

    /// <summary>Deletes a custom role. System roles cannot be deleted.</summary>
    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    [Authorize(Policy = Permissions.OrganizationRolesManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRole(Guid id, Guid roleId, CancellationToken cancellationToken)
    {
        await roleService.DeleteAsync(id, roleId, cancellationToken);

        return NoContent();
    }
}
