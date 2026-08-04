using System.Linq.Expressions;
using NSubstitute;
using StarterKit.Application.Common.Authorization;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Resources;
using StarterKit.Application.Services.Notifications;
using StarterKit.Application.Services.Organizations;
using StarterKit.Application.Tests.TestSupport;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Tests.Services.Organizations;

public class OrganizationMembershipServiceTests
{
    private sealed record Fixture(
        OrganizationMembershipService Service,
        IRepository<OrganizationMember, Guid> MemberRepo,
        IRepository<OrganizationMemberRole, Guid> MemberRoleRepo,
        IRepository<Role, Guid> RoleRepo,
        IRepository<Account, Guid> AccountRepo,
        IRepository<Organization, Guid> OrganizationRepo,
        IUnitOfWork UnitOfWork,
        ITenantAccessService TenantAccessService,
        IPermissionResolver PermissionResolver,
        INotificationService NotificationService);

    private static Fixture CreateFixture()
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<OrganizationMember, Guid> memberRepo = Substitute.For<IRepository<OrganizationMember, Guid>>();
        IRepository<OrganizationMemberRole, Guid> memberRoleRepo =
            Substitute.For<IRepository<OrganizationMemberRole, Guid>>();
        IRepository<Role, Guid> roleRepo = Substitute.For<IRepository<Role, Guid>>();
        IRepository<Account, Guid> accountRepo = Substitute.For<IRepository<Account, Guid>>();
        IRepository<Organization, Guid> organizationRepo = Substitute.For<IRepository<Organization, Guid>>();
        unitOfWork.Repository<OrganizationMember, Guid>().Returns(memberRepo);
        unitOfWork.Repository<OrganizationMemberRole, Guid>().Returns(memberRoleRepo);
        unitOfWork.Repository<Role, Guid>().Returns(roleRepo);
        unitOfWork.Repository<Account, Guid>().Returns(accountRepo);
        unitOfWork.Repository<Organization, Guid>().Returns(organizationRepo);

        // Defaults: empty lists — tests seed the specific rows a scenario needs.
        memberRoleRepo.ListAsync(Arg.Any<Expression<Func<OrganizationMemberRole, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        roleRepo.ListAsync(Arg.Any<Expression<Func<Role, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Default: any organization lookup resolves — success-path tests for AddMemberAsync need
        // this to reach the post-save notification step (which loads Organization for its name).
        organizationRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Organization.Create(new OrganizationParams("Acme", "acme")));

        ITenantAccessService tenantAccessService = Substitute.For<ITenantAccessService>();

        IPermissionResolver permissionResolver = Substitute.For<IPermissionResolver>();

        INotificationService notificationService = Substitute.For<INotificationService>();

        OrganizationMembershipService service = new(
            unitOfWork, tenantAccessService, permissionResolver, notificationService);

        return new Fixture(
            service,
            memberRepo,
            memberRoleRepo,
            roleRepo,
            accountRepo,
            organizationRepo,
            unitOfWork,
            tenantAccessService,
            permissionResolver,
            notificationService);
    }

    private static Account CreateAccount(string email = "member@example.com") =>
        Account.Create(new AccountParams("Member", $"member-{Guid.NewGuid():N}", email));

    [Fact]
    public async Task AddMemberAsync_EmptyRoleIds_ThrowsConflict()
    {
        Fixture f = CreateFixture();
        Guid organizationId = Guid.NewGuid();

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.OrganizationMemberRequiresAtLeastOneRole,
            () => f.Service.AddMemberAsync(organizationId, new AddMemberRequest("x@example.com", []), CancellationToken.None));
    }

    [Fact]
    public async Task AddMemberAsync_RoleIdFromAnotherOrganization_ThrowsNotFound()
    {
        Fixture f = CreateFixture();
        Guid organizationId = Guid.NewGuid();

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
        Fixture f = CreateFixture();
        Guid organizationId = Guid.NewGuid();

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
        Fixture f = CreateFixture();
        Guid organizationId = Guid.NewGuid();
        Account target = CreateAccount();

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
        Fixture f = CreateFixture();
        Guid organizationId = Guid.NewGuid();
        Account target = CreateAccount();

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
    public async Task AddMemberAsync_NewMember_SendsNotification()
    {
        Fixture f = CreateFixture();
        Guid organizationId = Guid.NewGuid();
        Account target = CreateAccount();
        Organization organization = Organization.Create(new OrganizationParams("Contoso", "contoso"));
        f.OrganizationRepo.GetByIdAsync(organizationId, Arg.Any<CancellationToken>()).Returns(organization);

        Role memberRole = Role.Create(new RoleParams(organizationId, "Member", SystemRoleKind.Member));
        RepositoryPredicateStub.StubListAsync(f.RoleRepo, [memberRole]);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(target);

        await f.Service.AddMemberAsync(
            organizationId, new AddMemberRequest(target.Email, [memberRole.Id]), CancellationToken.None);

        await f.NotificationService.Received(1).NotifyAsync(
            Arg.Is<NotificationParams>(p =>
                p.AccountId == target.Id
                && p.Type == NotificationTypes.OrganizationMemberAdded
                && p.Data != null && p.Data.Contains("Contoso")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddMemberAsync_OrganizationNotFound_ThrowsNotFound()
    {
        Fixture f = CreateFixture();
        Guid organizationId = Guid.NewGuid();
        Account target = CreateAccount();
        f.OrganizationRepo.GetByIdAsync(organizationId, Arg.Any<CancellationToken>()).Returns((Organization?)null);

        Role memberRole = Role.Create(new RoleParams(organizationId, "Member", SystemRoleKind.Member));
        RepositoryPredicateStub.StubListAsync(f.RoleRepo, [memberRole]);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(target);

        await ApplicationAssert.AssertNotFoundAsync<Organization>(
            organizationId,
            () => f.Service.AddMemberAsync(
                organizationId, new AddMemberRequest(target.Email, [memberRole.Id]), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateMemberRolesAsync_DemotingLastOwner_ThrowsConflict()
    {
        Fixture f = CreateFixture();
        Guid organizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();

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
        Fixture f = CreateFixture();
        Guid organizationId = Guid.NewGuid();
        Guid targetAccountId = Guid.NewGuid();

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
        Fixture f = CreateFixture();
        Guid organizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();

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
        Fixture f = CreateFixture();
        Guid organizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Guid targetAccountId = Guid.NewGuid();

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
}
