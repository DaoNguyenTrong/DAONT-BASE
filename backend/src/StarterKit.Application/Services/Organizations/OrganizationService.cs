using StarterKit.Application.Common.Authorization;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Resources;
using StarterKit.Application.Services.Roles;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Services.Organizations;

public sealed class OrganizationService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    ITenantAccessService tenantAccessService,
    IPermissionResolver permissionResolver,
    IRoleService roleService) : IOrganizationService
{
    public async Task<OrganizationDto> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();

        string slug = request.Slug.Trim().ToLowerInvariant();
        IRepository<Organization, Guid> organizationRepository = unitOfWork.Repository<Organization, Guid>();

        if (await organizationRepository.FirstOrDefaultAsync(o => o.Slug == slug, cancellationToken) is not null)
        {
            throw new ConflictException(ApplicationMessages.OrganizationSlugAlreadyExists);
        }

        Organization organization = Organization.Create(new OrganizationParams(request.Name, request.Slug));
        await organizationRepository.AddAsync(organization, cancellationToken);

        IReadOnlyDictionary<SystemRoleKind, Role> systemRoles =
            await roleService.SeedSystemRolesAsync(organization.Id, cancellationToken);
        Role ownerRole = systemRoles[SystemRoleKind.Owner];

        OrganizationMember owner = OrganizationMember.Create(new OrganizationMemberParams(organization.Id, accountId));
        await unitOfWork.Repository<OrganizationMember, Guid>().AddAsync(owner, cancellationToken);

        await unitOfWork.Repository<OrganizationMemberRole, Guid>().AddAsync(
            OrganizationMemberRole.Create(new OrganizationMemberRoleParams(owner.Id, ownerRole.Id)), cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new OrganizationDto(
            organization.Id, organization.Name, organization.Slug, organization.Status,
            [ownerRole.Name], Permissions.All);
    }

    public async Task<IReadOnlyList<OrganizationDto>> ListMineAsync(CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();

        IReadOnlyList<OrganizationMember> memberships = await unitOfWork.Repository<OrganizationMember, Guid>()
            .ListAsync(m => m.AccountId == accountId && m.IsActive, cancellationToken);

        Dictionary<Guid, List<Role>> rolesByMembershipId = await LoadRolesByMembershipIdAsync(
            memberships.Select(m => m.Id).ToList(), cancellationToken);

        ILookup<Guid, string> permissionCodesByRoleId = await LoadPermissionCodesByRoleIdAsync(
            rolesByMembershipId.Values.SelectMany(roles => roles), cancellationToken);

        IRepository<Organization, Guid> organizationRepository = unitOfWork.Repository<Organization, Guid>();
        List<OrganizationDto> result = [];

        foreach (OrganizationMember membership in memberships)
        {
            Organization? organization = await organizationRepository.GetByIdAsync(membership.OrganizationId, cancellationToken);

            if (organization is null)
            {
                continue;
            }

            List<Role> roles = rolesByMembershipId.GetValueOrDefault(membership.Id, []);
            bool isOwner = roles.Any(role => role.SystemRoleKind == SystemRoleKind.Owner);
            HashSet<string> permissionCodes = isOwner
                ? [.. Permissions.All]
                : roles.SelectMany(role => permissionCodesByRoleId[role.Id]).ToHashSet();

            result.Add(new OrganizationDto(
                organization.Id,
                organization.Name,
                organization.Slug,
                organization.Status,
                roles.Select(role => role.Name).ToList(),
                permissionCodes.ToList()));
        }

        return result;
    }

    public async Task<IReadOnlyList<OrganizationMemberDto>> GetMembersAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        IReadOnlyList<OrganizationMember> members = await unitOfWork.Repository<OrganizationMember, Guid>()
            .ListAsync(m => m.OrganizationId == organizationId && m.IsActive, cancellationToken);

        Dictionary<Guid, List<Role>> rolesByMembershipId = await LoadRolesByMembershipIdAsync(
            members.Select(m => m.Id).ToList(), cancellationToken);

        IRepository<Account, Guid> accountRepository = unitOfWork.Repository<Account, Guid>();
        List<OrganizationMemberDto> result = [];

        foreach (OrganizationMember member in members)
        {
            Account? account = await accountRepository.GetByIdAsync(member.AccountId, cancellationToken);

            if (account is not null)
            {
                List<Role> roles = rolesByMembershipId.GetValueOrDefault(member.Id, []);
                result.Add(new OrganizationMemberDto(
                    account.Id,
                    account.Name,
                    account.Email,
                    roles.Select(role => role.Id).ToList(),
                    roles.Select(role => role.Name).ToList()));
            }
        }

        return result;
    }

    public async Task DeactivateAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        IRepository<Organization, Guid> organizationRepository = unitOfWork.Repository<Organization, Guid>();
        Organization organization = await organizationRepository.GetByIdAsync(organizationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Organization), organizationId);

        organization.Deactivate();
        organizationRepository.Update(organization);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await tenantAccessService.InvalidateOrganizationAsync(organizationId, cancellationToken);
        await permissionResolver.InvalidateOrganizationAsync(organizationId, cancellationToken);
    }

    private async Task<Dictionary<Guid, List<Role>>> LoadRolesByMembershipIdAsync(
        IReadOnlyList<Guid> membershipIds, CancellationToken cancellationToken)
    {
        Dictionary<Guid, List<Role>> result = membershipIds.ToDictionary(id => id, _ => new List<Role>());

        if (membershipIds.Count == 0)
        {
            return result;
        }

        IReadOnlyList<OrganizationMemberRole> memberRoles = await unitOfWork.Repository<OrganizationMemberRole, Guid>()
            .ListAsync(mr => membershipIds.Contains(mr.OrganizationMemberId), cancellationToken);

        List<Guid> roleIds = memberRoles.Select(mr => mr.RoleId).Distinct().ToList();

        IReadOnlyList<Role> roles = roleIds.Count == 0
            ? []
            : await unitOfWork.Repository<Role, Guid>().ListAsync(role => roleIds.Contains(role.Id), cancellationToken);

        Dictionary<Guid, Role> roleById = roles.ToDictionary(role => role.Id);

        foreach (OrganizationMemberRole memberRole in memberRoles)
        {
            if (roleById.TryGetValue(memberRole.RoleId, out Role? role))
            {
                result[memberRole.OrganizationMemberId].Add(role);
            }
        }

        return result;
    }

    private async Task<ILookup<Guid, string>> LoadPermissionCodesByRoleIdAsync(
        IEnumerable<Role> roles, CancellationToken cancellationToken)
    {
        List<Guid> nonOwnerRoleIds = roles
            .Where(role => role.SystemRoleKind != SystemRoleKind.Owner)
            .Select(role => role.Id)
            .Distinct()
            .ToList();

        if (nonOwnerRoleIds.Count == 0)
        {
            return Array.Empty<RolePermission>().ToLookup(rp => rp.RoleId, rp => rp.PermissionCode);
        }

        IReadOnlyList<RolePermission> rolePermissions = await unitOfWork.Repository<RolePermission, Guid>()
            .ListAsync(rp => nonOwnerRoleIds.Contains(rp.RoleId), cancellationToken);

        return rolePermissions.ToLookup(rp => rp.RoleId, rp => rp.PermissionCode);
    }

    private Guid GetCurrentAccountId()
    {
        if (!Guid.TryParse(currentUserService.UserId, out Guid accountId))
        {
            throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);
        }

        return accountId;
    }
}
