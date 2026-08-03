using StarterKit.Application.Common.Authorization;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Resources;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Services.Roles;

public sealed class RoleService(
    IUnitOfWork unitOfWork,
    IPermissionResolver permissionResolver) : IRoleService
{
    public async Task<IReadOnlyList<RoleDto>> ListAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Role> roles = await unitOfWork.Repository<Role, Guid>()
            .ListAsync(role => role.OrganizationId == organizationId, cancellationToken);

        Dictionary<Guid, List<string>> permissionsByRole = await LoadPermissionsByRoleAsync(
            roles.Select(role => role.Id).ToList(), cancellationToken);

        return roles.Select(role => ToDto(role, permissionsByRole)).ToList();
    }

    public async Task<RoleDto> CreateAsync(
        Guid organizationId, CreateRoleRequest request, CancellationToken cancellationToken)
    {
        await EnsureUniqueNameAsync(organizationId, request.Name, null, cancellationToken);
        EnsureValidPermissionCodes(request.PermissionCodes);

        Role role = Role.Create(new RoleParams(organizationId, request.Name));
        await unitOfWork.Repository<Role, Guid>().AddAsync(role, cancellationToken);
        await AddRolePermissionsAsync(role.Id, request.PermissionCodes, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await permissionResolver.InvalidateOrganizationAsync(organizationId, cancellationToken);

        return new RoleDto(role.Id, role.Name, false, request.PermissionCodes.Distinct().ToList());
    }

    public async Task<RoleDto> UpdateAsync(
        Guid organizationId, Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        Role role = await GetOrgRoleAsync(organizationId, roleId, cancellationToken);
        EnsureNotSystem(role);
        await EnsureUniqueNameAsync(organizationId, request.Name, roleId, cancellationToken);
        EnsureValidPermissionCodes(request.PermissionCodes);

        role.Update(new RoleParams(organizationId, request.Name));
        unitOfWork.Repository<Role, Guid>().Update(role);

        IRepository<RolePermission, Guid> rolePermissionRepository = unitOfWork.Repository<RolePermission, Guid>();
        IReadOnlyList<RolePermission> existing = await rolePermissionRepository
            .ListAsync(rolePermission => rolePermission.RoleId == roleId, cancellationToken);

        foreach (RolePermission rolePermission in existing)
        {
            rolePermissionRepository.Delete(rolePermission);
        }

        await AddRolePermissionsAsync(roleId, request.PermissionCodes, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await permissionResolver.InvalidateOrganizationAsync(organizationId, cancellationToken);

        return new RoleDto(role.Id, role.Name, false, request.PermissionCodes.Distinct().ToList());
    }

    public async Task DeleteAsync(Guid organizationId, Guid roleId, CancellationToken cancellationToken)
    {
        Role role = await GetOrgRoleAsync(organizationId, roleId, cancellationToken);
        EnsureNotSystem(role);

        unitOfWork.Repository<Role, Guid>().Delete(role);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await permissionResolver.InvalidateOrganizationAsync(organizationId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<SystemRoleKind, Role>> SeedSystemRolesAsync(
        Guid organizationId, CancellationToken cancellationToken)
    {
        IRepository<Role, Guid> roleRepository = unitOfWork.Repository<Role, Guid>();
        Dictionary<SystemRoleKind, Role> roles = [];

        foreach (SystemRoleKind kind in Enum.GetValues<SystemRoleKind>())
        {
            Role role = Role.Create(new RoleParams(organizationId, kind.ToString(), kind));
            await roleRepository.AddAsync(role, cancellationToken);
            roles[kind] = role;

            if (SystemRoleDefaults.PermissionsByKind.TryGetValue(kind, out IReadOnlyList<string>? codes))
            {
                await AddRolePermissionsAsync(role.Id, codes, cancellationToken);
            }
        }

        return roles;
    }

    private async Task AddRolePermissionsAsync(
        Guid roleId, IReadOnlyList<string> permissionCodes, CancellationToken cancellationToken)
    {
        IRepository<RolePermission, Guid> rolePermissionRepository = unitOfWork.Repository<RolePermission, Guid>();

        foreach (string code in permissionCodes.Distinct())
        {
            await rolePermissionRepository.AddAsync(
                RolePermission.Create(new RolePermissionParams(roleId, code)), cancellationToken);
        }
    }

    private async Task<Dictionary<Guid, List<string>>> LoadPermissionsByRoleAsync(
        IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken)
    {
        Dictionary<Guid, List<string>> result = roleIds.ToDictionary(id => id, _ => new List<string>());

        if (roleIds.Count == 0)
        {
            return result;
        }

        IReadOnlyList<RolePermission> rolePermissions = await unitOfWork.Repository<RolePermission, Guid>()
            .ListAsync(rolePermission => roleIds.Contains(rolePermission.RoleId), cancellationToken);

        foreach (RolePermission rolePermission in rolePermissions)
        {
            result[rolePermission.RoleId].Add(rolePermission.PermissionCode);
        }

        return result;
    }

    private static RoleDto ToDto(Role role, IReadOnlyDictionary<Guid, List<string>> permissionsByRole)
    {
        IReadOnlyList<string> permissionCodes = role.SystemRoleKind == SystemRoleKind.Owner
            ? Permissions.All
            : permissionsByRole.TryGetValue(role.Id, out List<string>? codes) ? codes : [];

        return new RoleDto(role.Id, role.Name, role.SystemRoleKind is not null, permissionCodes);
    }

    private async Task<Role> GetOrgRoleAsync(Guid organizationId, Guid roleId, CancellationToken cancellationToken)
    {
        Role role = await unitOfWork.Repository<Role, Guid>().GetByIdAsync(roleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), roleId);

        if (role.OrganizationId != organizationId)
        {
            throw new NotFoundException(nameof(Role), roleId);
        }

        return role;
    }

    private static void EnsureNotSystem(Role role)
    {
        if (role.SystemRoleKind is not null)
        {
            throw new ForbiddenException(ApplicationMessages.OrganizationSystemRoleImmutable);
        }
    }

    private async Task EnsureUniqueNameAsync(
        Guid organizationId, string name, Guid? excludeRoleId, CancellationToken cancellationToken)
    {
        string trimmed = name.Trim();
        Role? existing = await unitOfWork.Repository<Role, Guid>().FirstOrDefaultAsync(
            role => role.OrganizationId == organizationId && role.Name == trimmed, cancellationToken);

        if (existing is not null && existing.Id != excludeRoleId)
        {
            throw new ConflictException(ApplicationMessages.OrganizationRoleNameAlreadyExists);
        }
    }

    private static void EnsureValidPermissionCodes(IReadOnlyList<string> permissionCodes)
    {
        foreach (string code in permissionCodes)
        {
            if (!Permissions.All.Contains(code))
            {
                throw new DomainException(ApplicationMessages.UnknownPermissionCode);
            }
        }
    }
}
