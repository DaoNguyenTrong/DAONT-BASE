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

    public async Task AddMemberAsync(Guid organizationId, AddMemberRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> roleIds = await ValidateRoleIdsAsync(organizationId, request.RoleIds, cancellationToken);

        Account account = await unitOfWork.Repository<Account, Guid>()
            .FirstOrDefaultAsync(a => a.Email == request.Email, cancellationToken)
            ?? throw new NotFoundException(ApplicationMessages.AccountNotFound);

        IRepository<OrganizationMember, Guid> memberRepository = unitOfWork.Repository<OrganizationMember, Guid>();

        OrganizationMember? existing = await memberRepository.FirstOrDefaultAsync(
            m => m.OrganizationId == organizationId && m.AccountId == account.Id, cancellationToken);

        if (existing is { IsActive: true })
        {
            throw new ConflictException(ApplicationMessages.OrganizationMemberAlreadyExists);
        }

        OrganizationMember member;

        if (existing is not null)
        {
            existing.Reactivate();
            memberRepository.Update(existing);
            member = existing;
        }
        else
        {
            member = OrganizationMember.Create(new OrganizationMemberParams(organizationId, account.Id));
            await memberRepository.AddAsync(member, cancellationToken);
        }

        await ReplaceMemberRolesAsync(member.Id, roleIds, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await tenantAccessService.InvalidateMemberAsync(organizationId, account.Id, cancellationToken);
        await permissionResolver.InvalidateMemberAsync(organizationId, account.Id, cancellationToken);
    }

    public async Task UpdateMemberRolesAsync(
        Guid organizationId,
        Guid accountId,
        UpdateMemberRolesRequest request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> roleIds = await ValidateRoleIdsAsync(organizationId, request.RoleIds, cancellationToken);

        IRepository<OrganizationMember, Guid> memberRepository = unitOfWork.Repository<OrganizationMember, Guid>();
        OrganizationMember member = await memberRepository.FirstOrDefaultAsync(
                m => m.OrganizationId == organizationId && m.AccountId == accountId && m.IsActive, cancellationToken)
            ?? throw new NotFoundException(nameof(OrganizationMember), accountId);

        Role ownerRole = await GetSystemRoleAsync(organizationId, SystemRoleKind.Owner, cancellationToken);
        bool currentlyOwner = await IsMemberInRoleAsync(member.Id, ownerRole.Id, cancellationToken);

        if (currentlyOwner && !roleIds.Contains(ownerRole.Id))
        {
            await EnsureNotLastOwnerAsync(ownerRole.Id, member.Id, cancellationToken);
        }

        await ReplaceMemberRolesAsync(member.Id, roleIds, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await tenantAccessService.InvalidateMemberAsync(organizationId, accountId, cancellationToken);
        await permissionResolver.InvalidateMemberAsync(organizationId, accountId, cancellationToken);
    }

    public async Task RemoveMemberAsync(Guid organizationId, Guid accountId, CancellationToken cancellationToken)
    {
        IRepository<OrganizationMember, Guid> memberRepository = unitOfWork.Repository<OrganizationMember, Guid>();
        OrganizationMember member = await memberRepository.FirstOrDefaultAsync(
                m => m.OrganizationId == organizationId && m.AccountId == accountId && m.IsActive, cancellationToken)
            ?? throw new NotFoundException(nameof(OrganizationMember), accountId);

        Role ownerRole = await GetSystemRoleAsync(organizationId, SystemRoleKind.Owner, cancellationToken);

        if (await IsMemberInRoleAsync(member.Id, ownerRole.Id, cancellationToken))
        {
            await EnsureNotLastOwnerAsync(ownerRole.Id, member.Id, cancellationToken);
        }

        member.Deactivate();
        memberRepository.Update(member);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await tenantAccessService.InvalidateMemberAsync(organizationId, accountId, cancellationToken);
        await permissionResolver.InvalidateMemberAsync(organizationId, accountId, cancellationToken);
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

    private async Task EnsureNotLastOwnerAsync(Guid ownerRoleId, Guid excludeMemberId, CancellationToken cancellationToken)
    {
        IReadOnlyList<OrganizationMemberRole> ownerAssignments = await unitOfWork.Repository<OrganizationMemberRole, Guid>()
            .ListAsync(mr => mr.RoleId == ownerRoleId && mr.OrganizationMemberId != excludeMemberId, cancellationToken);

        if (ownerAssignments.Count == 0)
        {
            throw new ConflictException(ApplicationMessages.OrganizationCannotRemoveLastOwner);
        }

        List<Guid> memberIds = ownerAssignments.Select(mr => mr.OrganizationMemberId).Distinct().ToList();

        IReadOnlyList<OrganizationMember> otherActiveOwners = await unitOfWork.Repository<OrganizationMember, Guid>()
            .ListAsync(m => memberIds.Contains(m.Id) && m.IsActive, cancellationToken);

        if (otherActiveOwners.Count == 0)
        {
            throw new ConflictException(ApplicationMessages.OrganizationCannotRemoveLastOwner);
        }
    }

    private async Task<Role> GetSystemRoleAsync(Guid organizationId, SystemRoleKind kind, CancellationToken cancellationToken)
    {
        return await unitOfWork.Repository<Role, Guid>().FirstOrDefaultAsync(
                role => role.OrganizationId == organizationId && role.SystemRoleKind == kind, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), organizationId);
    }

    private async Task<bool> IsMemberInRoleAsync(Guid memberId, Guid roleId, CancellationToken cancellationToken)
    {
        return await unitOfWork.Repository<OrganizationMemberRole, Guid>().FirstOrDefaultAsync(
            mr => mr.OrganizationMemberId == memberId && mr.RoleId == roleId, cancellationToken) is not null;
    }

    private async Task<IReadOnlyList<Guid>> ValidateRoleIdsAsync(
        Guid organizationId, IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0)
        {
            throw new ConflictException(ApplicationMessages.OrganizationMemberRequiresAtLeastOneRole);
        }

        List<Guid> distinctRoleIds = roleIds.Distinct().ToList();

        IReadOnlyList<Role> roles = await unitOfWork.Repository<Role, Guid>().ListAsync(
            role => distinctRoleIds.Contains(role.Id) && role.OrganizationId == organizationId, cancellationToken);

        if (roles.Count != distinctRoleIds.Count)
        {
            Guid missingRoleId = distinctRoleIds.First(id => roles.All(role => role.Id != id));
            throw new NotFoundException(nameof(Role), missingRoleId);
        }

        return distinctRoleIds;
    }

    private async Task ReplaceMemberRolesAsync(Guid memberId, IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken)
    {
        IRepository<OrganizationMemberRole, Guid> memberRoleRepository = unitOfWork.Repository<OrganizationMemberRole, Guid>();

        IReadOnlyList<OrganizationMemberRole> existing = await memberRoleRepository
            .ListAsync(mr => mr.OrganizationMemberId == memberId, cancellationToken);

        foreach (OrganizationMemberRole memberRole in existing)
        {
            memberRoleRepository.Delete(memberRole);
        }

        foreach (Guid roleId in roleIds)
        {
            await memberRoleRepository.AddAsync(
                OrganizationMemberRole.Create(new OrganizationMemberRoleParams(memberId, roleId)), cancellationToken);
        }
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
