using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Resources;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Services.Organizations;

public sealed class OrganizationService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    ITenantAccessService tenantAccessService) : IOrganizationService
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

        OrganizationMember owner = OrganizationMember.Create(
            new OrganizationMemberParams(organization.Id, accountId, OrganizationRole.Owner));
        await unitOfWork.Repository<OrganizationMember, Guid>().AddAsync(owner, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new OrganizationDto(organization.Id, organization.Name, organization.Slug, organization.Status, OrganizationRole.Owner);
    }

    public async Task<IReadOnlyList<OrganizationDto>> ListMineAsync(CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();

        IReadOnlyList<OrganizationMember> memberships = await unitOfWork.Repository<OrganizationMember, Guid>()
            .ListAsync(m => m.AccountId == accountId && m.IsActive, cancellationToken);

        IRepository<Organization, Guid> organizationRepository = unitOfWork.Repository<Organization, Guid>();
        List<OrganizationDto> result = [];

        foreach (OrganizationMember membership in memberships)
        {
            Organization? organization = await organizationRepository.GetByIdAsync(membership.OrganizationId, cancellationToken);

            if (organization is not null)
            {
                result.Add(new OrganizationDto(organization.Id, organization.Name, organization.Slug, organization.Status, membership.Role));
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<OrganizationMemberDto>> GetMembersAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await EnsureActiveMemberAsync(organizationId, cancellationToken);

        IReadOnlyList<OrganizationMember> members = await unitOfWork.Repository<OrganizationMember, Guid>()
            .ListAsync(m => m.OrganizationId == organizationId && m.IsActive, cancellationToken);

        IRepository<Account, Guid> accountRepository = unitOfWork.Repository<Account, Guid>();
        List<OrganizationMemberDto> result = [];

        foreach (OrganizationMember member in members)
        {
            Account? account = await accountRepository.GetByIdAsync(member.AccountId, cancellationToken);

            if (account is not null)
            {
                result.Add(new OrganizationMemberDto(account.Id, account.Name, account.Email, member.Role));
            }
        }

        return result;
    }

    public async Task AddMemberAsync(Guid organizationId, AddMemberRequest request, CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(organizationId, cancellationToken);

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

        if (existing is not null)
        {
            existing.Update(new OrganizationMemberParams(organizationId, account.Id, request.Role));
            existing.Reactivate();
            memberRepository.Update(existing);
        }
        else
        {
            OrganizationMember member = OrganizationMember.Create(
                new OrganizationMemberParams(organizationId, account.Id, request.Role));
            await memberRepository.AddAsync(member, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await tenantAccessService.InvalidateMemberAsync(organizationId, account.Id, cancellationToken);
    }

    public async Task UpdateMemberRoleAsync(
        Guid organizationId,
        Guid accountId,
        UpdateMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(organizationId, cancellationToken);

        IRepository<OrganizationMember, Guid> memberRepository = unitOfWork.Repository<OrganizationMember, Guid>();
        OrganizationMember member = await memberRepository.FirstOrDefaultAsync(
                m => m.OrganizationId == organizationId && m.AccountId == accountId && m.IsActive, cancellationToken)
            ?? throw new NotFoundException(nameof(OrganizationMember), accountId);

        if (member.Role == OrganizationRole.Owner && request.Role != OrganizationRole.Owner)
        {
            await EnsureNotLastOwnerAsync(organizationId, member.Id, cancellationToken);
        }

        member.Update(new OrganizationMemberParams(organizationId, accountId, request.Role));
        memberRepository.Update(member);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await tenantAccessService.InvalidateMemberAsync(organizationId, accountId, cancellationToken);
    }

    public async Task RemoveMemberAsync(Guid organizationId, Guid accountId, CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(organizationId, cancellationToken);

        IRepository<OrganizationMember, Guid> memberRepository = unitOfWork.Repository<OrganizationMember, Guid>();
        OrganizationMember member = await memberRepository.FirstOrDefaultAsync(
                m => m.OrganizationId == organizationId && m.AccountId == accountId && m.IsActive, cancellationToken)
            ?? throw new NotFoundException(nameof(OrganizationMember), accountId);

        if (member.Role == OrganizationRole.Owner)
        {
            await EnsureNotLastOwnerAsync(organizationId, member.Id, cancellationToken);
        }

        member.Deactivate();
        memberRepository.Update(member);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await tenantAccessService.InvalidateMemberAsync(organizationId, accountId, cancellationToken);
    }


    public async Task DeactivateAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        OrganizationMember member = await EnsureActiveMemberAsync(organizationId, cancellationToken);

        if (member.Role != OrganizationRole.Owner)
        {
            throw new ForbiddenException(ApplicationMessages.OrganizationAccessDenied);
        }

        IRepository<Organization, Guid> organizationRepository = unitOfWork.Repository<Organization, Guid>();
        Organization organization = await organizationRepository.GetByIdAsync(organizationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Organization), organizationId);

        organization.Deactivate();
        organizationRepository.Update(organization);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await tenantAccessService.InvalidateOrganizationAsync(organizationId, cancellationToken);
    }

    private async Task EnsureNotLastOwnerAsync(Guid organizationId, Guid excludeMemberId, CancellationToken cancellationToken)
    {
        IReadOnlyList<OrganizationMember> owners = await unitOfWork.Repository<OrganizationMember, Guid>()
            .ListAsync(
                m => m.OrganizationId == organizationId && m.IsActive && m.Role == OrganizationRole.Owner,
                cancellationToken);

        if (owners.Count(o => o.Id != excludeMemberId) == 0)
        {
            throw new ConflictException(ApplicationMessages.OrganizationCannotRemoveLastOwner);
        }
    }

    private async Task<OrganizationMember> EnsureActiveMemberAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();

        return await unitOfWork.Repository<OrganizationMember, Guid>().FirstOrDefaultAsync(
                m => m.OrganizationId == organizationId && m.AccountId == accountId && m.IsActive, cancellationToken)
            ?? throw new ForbiddenException(ApplicationMessages.OrganizationAccessDenied);
    }

    private async Task EnsureManagerAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        OrganizationMember member = await EnsureActiveMemberAsync(organizationId, cancellationToken);

        if (member.Role is not (OrganizationRole.Owner or OrganizationRole.Admin))
        {
            throw new ForbiddenException(ApplicationMessages.OrganizationAccessDenied);
        }
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
