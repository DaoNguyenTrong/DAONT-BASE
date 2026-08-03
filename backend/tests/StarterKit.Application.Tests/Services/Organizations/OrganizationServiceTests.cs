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
        IRepository<Role, Guid> RoleRepo,
        IRepository<RolePermission, Guid> RolePermissionRepo,
        IRepository<Account, Guid> AccountRepo,
        IUnitOfWork UnitOfWork,
        ICurrentUserService CurrentUserService,
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
        IRepository<Role, Guid> roleRepo = Substitute.For<IRepository<Role, Guid>>();
        IRepository<RolePermission, Guid> rolePermissionRepo = Substitute.For<IRepository<RolePermission, Guid>>();
        IRepository<Account, Guid> accountRepo = Substitute.For<IRepository<Account, Guid>>();
        unitOfWork.Repository<Organization, Guid>().Returns(organizationRepo);
        unitOfWork.Repository<OrganizationMember, Guid>().Returns(memberRepo);
        unitOfWork.Repository<OrganizationMemberRole, Guid>().Returns(memberRoleRepo);
        unitOfWork.Repository<Role, Guid>().Returns(roleRepo);
        unitOfWork.Repository<RolePermission, Guid>().Returns(rolePermissionRepo);
        unitOfWork.Repository<Account, Guid>().Returns(accountRepo);

        // Defaults: empty lists — tests seed the specific rows a scenario needs.
        memberRoleRepo.ListAsync(Arg.Any<Expression<Func<OrganizationMemberRole, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        roleRepo.ListAsync(Arg.Any<Expression<Func<Role, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        rolePermissionRepo.ListAsync(Arg.Any<Expression<Func<RolePermission, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([]);

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
            roleRepo,
            rolePermissionRepo,
            accountRepo,
            unitOfWork,
            currentUserService,
            tenantAccessService,
            permissionResolver,
            roleService);
    }

    private static Account CreateAccount(string email = "member@example.com") =>
        Account.Create(new AccountParams("Member", $"member-{Guid.NewGuid():N}", email));

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
    public async Task AddMemberAsync_EmptyRoleIds_ThrowsConflict()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        Guid organizationId = Guid.NewGuid();
        GrantPermission(f, organizationId, accountId, Permissions.OrganizationMembersManage);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.OrganizationMemberRequiresAtLeastOneRole,
            () => f.Service.AddMemberAsync(organizationId, new AddMemberRequest("x@example.com", []), CancellationToken.None));
    }

    [Fact]
    public async Task AddMemberAsync_RoleIdFromAnotherOrganization_ThrowsNotFound()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        Guid organizationId = Guid.NewGuid();
        GrantPermission(f, organizationId, accountId, Permissions.OrganizationMembersManage);

        Role foreignRole = Role.Create(new RoleParams(Guid.NewGuid(), "Member", SystemRoleKind.Member));
        RepositoryPredicateStub.StubListAsync(f.RoleRepo, [foreignRole]);

        await ApplicationAssert.AssertNotFoundAsync<Role>(
            foreignRole.Id,
            () => f.Service.AddMemberAsync(
                organizationId, new AddMemberRequest("x@example.com", [foreignRole.Id]), CancellationToken.None));
    }

    [Fact]
    public async Task AddMemberAsync_UnknownEmail_ThrowsNotFound()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        Guid organizationId = Guid.NewGuid();
        GrantPermission(f, organizationId, accountId, Permissions.OrganizationMembersManage);

        Role memberRole = Role.Create(new RoleParams(organizationId, "Member", SystemRoleKind.Member));
        RepositoryPredicateStub.StubListAsync(f.RoleRepo, [memberRole]);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Account?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<NotFoundException>(
            ApplicationMessages.AccountNotFound,
            () => f.Service.AddMemberAsync(
                organizationId, new AddMemberRequest("nobody@example.com", [memberRole.Id]), CancellationToken.None));
    }

    [Fact]
    public async Task AddMemberAsync_AlreadyActiveMember_ThrowsConflict()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        Guid organizationId = Guid.NewGuid();
        Account target = CreateAccount();
        GrantPermission(f, organizationId, accountId, Permissions.OrganizationMembersManage);

        Role memberRole = Role.Create(new RoleParams(organizationId, "Member", SystemRoleKind.Member));
        RepositoryPredicateStub.StubListAsync(f.RoleRepo, [memberRole]);

        OrganizationMember targetMembership = OrganizationMember.Create(new OrganizationMemberParams(organizationId, target.Id));
        RepositoryPredicateStub.StubFirstOrDefault(f.MemberRepo, [targetMembership]);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(target);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.OrganizationMemberAlreadyExists,
            () => f.Service.AddMemberAsync(
                organizationId, new AddMemberRequest(target.Email, [memberRole.Id]), CancellationToken.None));
    }

    [Fact]
    public async Task AddMemberAsync_PreviouslyRemovedMember_ReactivatesWithNewRoles()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        Guid organizationId = Guid.NewGuid();
        Account target = CreateAccount();
        GrantPermission(f, organizationId, accountId, Permissions.OrganizationMembersManage);

        Role adminRole = Role.Create(new RoleParams(organizationId, "Admin", SystemRoleKind.Admin));
        RepositoryPredicateStub.StubListAsync(f.RoleRepo, [adminRole]);

        OrganizationMember removedMembership = OrganizationMember.Create(new OrganizationMemberParams(organizationId, target.Id));
        removedMembership.Deactivate();
        RepositoryPredicateStub.StubFirstOrDefault(f.MemberRepo, [removedMembership]);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(target);

        await f.Service.AddMemberAsync(
            organizationId, new AddMemberRequest(target.Email, [adminRole.Id]), CancellationToken.None);

        Assert.True(removedMembership.IsActive);
        f.MemberRepo.Received(1).Update(removedMembership);
        await f.MemberRoleRepo.Received(1).AddAsync(
            Arg.Is<OrganizationMemberRole>(mr => mr.RoleId == adminRole.Id && mr.OrganizationMemberId == removedMembership.Id),
            Arg.Any<CancellationToken>());
        await f.TenantAccessService.Received(1).InvalidateMemberAsync(organizationId, target.Id, Arg.Any<CancellationToken>());
        await f.PermissionResolver.Received(1).InvalidateMemberAsync(organizationId, target.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateMemberRolesAsync_DemotingLastOwner_ThrowsConflict()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        Guid organizationId = Guid.NewGuid();
        GrantPermission(f, organizationId, accountId, Permissions.OrganizationMembersManage);

        Role ownerRole = Role.Create(new RoleParams(organizationId, "Owner", SystemRoleKind.Owner));
        Role adminRole = Role.Create(new RoleParams(organizationId, "Admin", SystemRoleKind.Admin));
        RepositoryPredicateStub.StubFirstOrDefault(f.RoleRepo, [ownerRole, adminRole]);
        RepositoryPredicateStub.StubListAsync(f.RoleRepo, [ownerRole, adminRole]);

        OrganizationMember onlyOwner = OrganizationMember.Create(new OrganizationMemberParams(organizationId, accountId));
        RepositoryPredicateStub.StubFirstOrDefault(f.MemberRepo, [onlyOwner]);

        OrganizationMemberRole ownerAssignment = OrganizationMemberRole.Create(
            new OrganizationMemberRoleParams(onlyOwner.Id, ownerRole.Id));
        RepositoryPredicateStub.StubFirstOrDefault(f.MemberRoleRepo, [ownerAssignment]);
        RepositoryPredicateStub.StubListAsync(f.MemberRoleRepo, [ownerAssignment]);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.OrganizationCannotRemoveLastOwner,
            () => f.Service.UpdateMemberRolesAsync(
                organizationId, accountId, new UpdateMemberRolesRequest([adminRole.Id]), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateMemberRolesAsync_NotLastOwner_ReplacesRolesAndInvalidatesCaches()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        Guid organizationId = Guid.NewGuid();
        Guid targetAccountId = Guid.NewGuid();
        GrantPermission(f, organizationId, accountId, Permissions.OrganizationMembersManage);

        Role ownerRole = Role.Create(new RoleParams(organizationId, "Owner", SystemRoleKind.Owner));
        Role adminRole = Role.Create(new RoleParams(organizationId, "Admin", SystemRoleKind.Admin));
        RepositoryPredicateStub.StubFirstOrDefault(f.RoleRepo, [ownerRole, adminRole]);
        RepositoryPredicateStub.StubListAsync(f.RoleRepo, [ownerRole, adminRole]);

        OrganizationMember target = OrganizationMember.Create(new OrganizationMemberParams(organizationId, targetAccountId));
        RepositoryPredicateStub.StubFirstOrDefault(f.MemberRepo, [target]);

        // target currently holds Member (never Owner) — the last-owner guard must not trigger.
        Role memberRole = Role.Create(new RoleParams(organizationId, "Member", SystemRoleKind.Member));
        OrganizationMemberRole currentAssignment = OrganizationMemberRole.Create(
            new OrganizationMemberRoleParams(target.Id, memberRole.Id));
        RepositoryPredicateStub.StubFirstOrDefault(f.MemberRoleRepo, [currentAssignment]);
        RepositoryPredicateStub.StubListAsync(f.MemberRoleRepo, [currentAssignment]);

        await f.Service.UpdateMemberRolesAsync(
            organizationId, targetAccountId, new UpdateMemberRolesRequest([adminRole.Id]), CancellationToken.None);

        await f.MemberRoleRepo.Received(1).AddAsync(
            Arg.Is<OrganizationMemberRole>(mr => mr.RoleId == adminRole.Id && mr.OrganizationMemberId == target.Id),
            Arg.Any<CancellationToken>());
        await f.TenantAccessService.Received(1).InvalidateMemberAsync(organizationId, targetAccountId, Arg.Any<CancellationToken>());
        await f.PermissionResolver.Received(1).InvalidateMemberAsync(organizationId, targetAccountId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveMemberAsync_LastOwner_ThrowsConflict()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        Guid organizationId = Guid.NewGuid();
        GrantPermission(f, organizationId, accountId, Permissions.OrganizationMembersManage);

        Role ownerRole = Role.Create(new RoleParams(organizationId, "Owner", SystemRoleKind.Owner));
        RepositoryPredicateStub.StubFirstOrDefault(f.RoleRepo, [ownerRole]);

        OrganizationMember onlyOwner = OrganizationMember.Create(new OrganizationMemberParams(organizationId, accountId));
        RepositoryPredicateStub.StubFirstOrDefault(f.MemberRepo, [onlyOwner]);

        OrganizationMemberRole ownerAssignment = OrganizationMemberRole.Create(
            new OrganizationMemberRoleParams(onlyOwner.Id, ownerRole.Id));
        RepositoryPredicateStub.StubFirstOrDefault(f.MemberRoleRepo, [ownerAssignment]);
        RepositoryPredicateStub.StubListAsync(f.MemberRoleRepo, [ownerAssignment]);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.OrganizationCannotRemoveLastOwner,
            () => f.Service.RemoveMemberAsync(organizationId, accountId, CancellationToken.None));

        Assert.True(onlyOwner.IsActive);
    }

    [Fact]
    public async Task RemoveMemberAsync_NotLastOwner_DeactivatesAndInvalidatesCache()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        Guid organizationId = Guid.NewGuid();
        Guid targetAccountId = Guid.NewGuid();
        GrantPermission(f, organizationId, accountId, Permissions.OrganizationMembersManage);

        Role memberRole = Role.Create(new RoleParams(organizationId, "Member", SystemRoleKind.Member));
        Role ownerRole = Role.Create(new RoleParams(organizationId, "Owner", SystemRoleKind.Owner));
        RepositoryPredicateStub.StubFirstOrDefault(f.RoleRepo, [memberRole, ownerRole]);

        OrganizationMember owner = OrganizationMember.Create(new OrganizationMemberParams(organizationId, accountId));
        OrganizationMember target = OrganizationMember.Create(new OrganizationMemberParams(organizationId, targetAccountId));
        RepositoryPredicateStub.StubFirstOrDefault(f.MemberRepo, [owner, target]);

        OrganizationMemberRole targetAssignment = OrganizationMemberRole.Create(
            new OrganizationMemberRoleParams(target.Id, memberRole.Id));
        RepositoryPredicateStub.StubFirstOrDefault(f.MemberRoleRepo, [targetAssignment]);

        await f.Service.RemoveMemberAsync(organizationId, targetAccountId, CancellationToken.None);

        Assert.False(target.IsActive);
        f.MemberRepo.Received(1).Update(target);
        await f.TenantAccessService.Received(1).InvalidateMemberAsync(organizationId, targetAccountId, Arg.Any<CancellationToken>());
        await f.PermissionResolver.Received(1).InvalidateMemberAsync(organizationId, targetAccountId, Arg.Any<CancellationToken>());
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
