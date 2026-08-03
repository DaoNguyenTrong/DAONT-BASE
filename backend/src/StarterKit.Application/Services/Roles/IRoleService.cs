using StarterKit.Domain.Entities;

namespace StarterKit.Application.Services.Roles;

public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> ListAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<RoleDto> CreateAsync(Guid organizationId, CreateRoleRequest request, CancellationToken cancellationToken);

    Task<RoleDto> UpdateAsync(
        Guid organizationId, Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid organizationId, Guid roleId, CancellationToken cancellationToken);

    /// <summary>
    /// Stages (does not save) the 3 system roles for a newly created organization, returning them
    /// keyed by kind so the caller can link the creating member to the seeded Owner role in the
    /// same unit of work.
    /// </summary>
    Task<IReadOnlyDictionary<SystemRoleKind, Role>> SeedSystemRolesAsync(
        Guid organizationId, CancellationToken cancellationToken);
}
