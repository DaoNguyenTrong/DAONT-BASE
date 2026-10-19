using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Services.Roles;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Services.DevData;

public sealed class DevDataSeeder(
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IRoleService roleService) : IDevDataSeeder
{
    public async Task SeedAsync(DevSeedRequest request, CancellationToken cancellationToken)
    {
        IRepository<Organization, Guid> organizationRepository = unitOfWork.Repository<Organization, Guid>();

        if (await organizationRepository.FirstOrDefaultAsync(o => o.Slug == request.OrganizationSlug, cancellationToken)
            is not null)
        {
            return;
        }

        Account owner = await EnsureAccountAsync(request.Owner, cancellationToken);
        Account member = await EnsureAccountAsync(request.Member, cancellationToken);

        Organization organization = Organization.Create(new OrganizationParams(request.OrganizationName, request.OrganizationSlug));
        await organizationRepository.AddAsync(organization, cancellationToken);

        IReadOnlyDictionary<SystemRoleKind, Role> systemRoles =
            await roleService.SeedSystemRolesAsync(organization.Id, cancellationToken);

        await AddMemberAsync(organization.Id, owner.Id, systemRoles[SystemRoleKind.Owner].Id, cancellationToken);
        await AddMemberAsync(organization.Id, member.Id, systemRoles[SystemRoleKind.Member].Id, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Account> EnsureAccountAsync(DevSeedAccount seed, CancellationToken cancellationToken)
    {
        IRepository<Account, Guid> accountRepository = unitOfWork.Repository<Account, Guid>();
        string username = Account.NormalizeUsername(seed.Username);

        Account? existing = await accountRepository.FirstOrDefaultAsync(a => a.Username == username, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        // Verified up front: the seed accounts exist to log in, not to exercise the registration flow.
        Account account = Account.Create(new AccountParams(seed.Name, seed.Username, seed.Email));
        account.SetPasswordHash(passwordHasher.Hash(seed.Password));
        account.ConfirmEmail();

        await accountRepository.AddAsync(account, cancellationToken);

        return account;
    }

    private async Task AddMemberAsync(Guid organizationId, Guid accountId, Guid roleId, CancellationToken cancellationToken)
    {
        OrganizationMember member = OrganizationMember.Create(new OrganizationMemberParams(organizationId, accountId));
        await unitOfWork.Repository<OrganizationMember, Guid>().AddAsync(member, cancellationToken);

        await unitOfWork.Repository<OrganizationMemberRole, Guid>().AddAsync(
            OrganizationMemberRole.Create(new OrganizationMemberRoleParams(member.Id, roleId)), cancellationToken);
    }
}
