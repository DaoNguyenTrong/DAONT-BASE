using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StarterKit.Application.Services.Organizations;

namespace StarterKit.API.Controllers;

[ApiController]
[Authorize]
[Route("api/organizations")]
public sealed class OrganizationsController(IOrganizationService organizationService) : ControllerBase
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
        await organizationService.AddMemberAsync(id, request, cancellationToken);

        return NoContent();
    }

    /// <summary>Changes a member's role. The caller must be an Owner or Admin.</summary>
    [HttpPatch("{id:guid}/members/{accountId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateMemberRole(
        Guid id,
        Guid accountId,
        UpdateMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        await organizationService.UpdateMemberRoleAsync(id, accountId, request, cancellationToken);

        return NoContent();
    }

    /// <summary>Removes a member from the organization. The caller must be an Owner or Admin.</summary>
    [HttpDelete("{id:guid}/members/{accountId:guid}")]
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
        await organizationService.RemoveMemberAsync(id, accountId, cancellationToken);

        return NoContent();
    }


    /// <summary>Deactivates an organization. The caller must be its Owner; all members immediately lose access.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await organizationService.DeactivateAsync(id, cancellationToken);

        return NoContent();
    }
}
