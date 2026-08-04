using System.Text.Json;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Resources;
using StarterKit.Application.Services.Notifications;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Services.Organizations;

public sealed class OrganizationMembershipService(
    IUnitOfWork unitOfWork,
    ITenantAccessService tenantAccessService,
    IPermissionResolver permissionResolver,
    INotificationService notificationService) : IOrganizationMembershipService
{
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

        Organization organization = await unitOfWork.Repository<Organization, Guid>()
            .GetByIdAsync(organizationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Organization), organizationId);

        await notificationService.NotifyAsync(
            new NotificationParams(
                account.Id,
                NotificationTypes.OrganizationMemberAdded,
                JsonSerializer.Serialize(new { organizationId, organizationName = organization.Name })),
            cancellationToken);
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
}
