using System.Linq.Expressions;
using NSubstitute;
using StarterKit.Application.Common.Authorization;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Resources;
using StarterKit.Application.Services.Organizations;
using StarterKit.Application.Services.Roles;
using StarterKit.Application.Tests.TestSupport;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Tests.Services.Organizations;

public class OrganizationServiceTests
{
    private sealed record Fixture(
        OrganizationService Service,
        IRepository<Organization, Guid> OrganizationRepo,
        IRepository<OrganizationMember, Guid> MemberRepo,
        IRepository<OrganizationMemberRole, Guid> MemberRoleRepo,
        IUnitOfWork UnitOfWork,
        ITenantAccessService TenantAccessService,
        IPermissionResolver PermissionResolver,
        IRoleService RoleService);

    private static Fixture CreateFixture(Guid? currentAccountId = null)
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<Organization, Guid> organizationRepo = Substitute.For<IRepository<Organization, Guid>>();
        IRepository<OrganizationMember, Guid> memberRepo = Substitute.For<IRepository<OrganizationMember, Guid>>();
        IRepository<OrganizationMemberRole, Guid> memberRoleRepo =
            Substitute.For<IRepository<OrganizationMemberRole, Guid>>();
        unitOfWork.Repository<Organization, Guid>().Returns(organizationRepo);
        unitOfWork.Repository<OrganizationMember, Guid>().Returns(memberRepo);
        unitOfWork.Repository<OrganizationMemberRole, Guid>().Returns(memberRoleRepo);

        ICurrentUserService currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns((currentAccountId ?? Guid.NewGuid()).ToString());

        ITenantAccessService tenantAccessService = Substitute.For<ITenantAccessService>();

        // Default: caller has no permissions in any organization unless a test grants one —
        // matches an account with no active membership, the common "forbidden" baseline.
        IPermissionResolver permissionResolver = Substitute.For<IPermissionResolver>();
        permissionResolver.GetEffectivePermissionsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlySet<string>)new HashSet<string>());

        IRoleService roleService = Substitute.For<IRoleService>();

        OrganizationService service = new(unitOfWork, currentUserService, tenantAccessService, permissionResolver, roleService);

        return new Fixture(
            service,
            organizationRepo,
            memberRepo,
            memberRoleRepo,
            unitOfWork,
            tenantAccessService,
            permissionResolver,
            roleService);
    }

    private static void GrantPermission(Fixture f, Guid organizationId, Guid accountId, string permissionCode) =>
        f.PermissionResolver.GetEffectivePermissionsAsync(organizationId, accountId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlySet<string>)new HashSet<string> { permissionCode });

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesOrganizationAndAddsCallerAsOwner()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        f.OrganizationRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Organization, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Organization?)null);

        Role ownerRole = Role.Create(new RoleParams(Guid.NewGuid(), "Owner", SystemRoleKind.Owner));
        f.RoleService.SeedSystemRolesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<SystemRoleKind, Role> { [SystemRoleKind.Owner] = ownerRole });

        CreateOrganizationRequest request = new("Acme Inc", "acme");

        OrganizationDto result = await f.Service.CreateAsync(request, CancellationToken.None);

        Assert.Equal("Acme Inc", result.Name);
        Assert.Equal(["Owner"], result.MyRoleNames);
        Assert.Equal(Permissions.All, result.MyPermissionCodes);
        await f.OrganizationRepo.Received(1).AddAsync(Arg.Any<Organization>(), Arg.Any<CancellationToken>());
        await f.MemberRepo.Received(1).AddAsync(
            Arg.Is<OrganizationMember>(m => m != null && m.AccountId == accountId), Arg.Any<CancellationToken>());
        await f.MemberRoleRepo.Received(1).AddAsync(
            Arg.Is<OrganizationMemberRole>(mr => mr.RoleId == ownerRole.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_SlugAlreadyExists_ThrowsConflict()
    {
        Fixture f = CreateFixture();
        Organization existing = Organization.Create(new OrganizationParams("Existing", "acme"));
        RepositoryPredicateStub.StubFirstOrDefault(f.OrganizationRepo, [existing]);
        CreateOrganizationRequest request = new("Acme Inc", "acme");

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.OrganizationSlugAlreadyExists,
            () => f.Service.CreateAsync(request, CancellationToken.None));

        await f.OrganizationRepo.DidNotReceive().AddAsync(Arg.Any<Organization>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateAsync_CallerHasOrganizationManagePermission_DeactivatesAndInvalidatesOrganizationCache()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        Guid organizationId = Guid.NewGuid();
        GrantPermission(f, organizationId, accountId, Permissions.OrganizationManage);

        Organization organization = Organization.Create(new OrganizationParams("Acme Inc", "acme"));
        f.OrganizationRepo.GetByIdAsync(organizationId, Arg.Any<CancellationToken>()).Returns(organization);

        await f.Service.DeactivateAsync(organizationId, CancellationToken.None);

        Assert.False(organization.Status);
        f.OrganizationRepo.Received(1).Update(organization);
        await f.TenantAccessService.Received(1).InvalidateOrganizationAsync(organizationId, Arg.Any<CancellationToken>());
        await f.PermissionResolver.Received(1).InvalidateOrganizationAsync(organizationId, Arg.Any<CancellationToken>());
    }
}
