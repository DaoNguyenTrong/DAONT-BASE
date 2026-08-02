using System.Linq.Expressions;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Resources;
using StarterKit.Application.Services.Organizations;
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
        IRepository<Account, Guid> AccountRepo,
        IUnitOfWork UnitOfWork,
        ICurrentUserService CurrentUserService,
        ITenantAccessService TenantAccessService);

    private static Fixture CreateFixture(Guid? currentAccountId = null)
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<Organization, Guid> organizationRepo = Substitute.For<IRepository<Organization, Guid>>();
        IRepository<OrganizationMember, Guid> memberRepo = Substitute.For<IRepository<OrganizationMember, Guid>>();
        IRepository<Account, Guid> accountRepo = Substitute.For<IRepository<Account, Guid>>();
        unitOfWork.Repository<Organization, Guid>().Returns(organizationRepo);
        unitOfWork.Repository<OrganizationMember, Guid>().Returns(memberRepo);
        unitOfWork.Repository<Account, Guid>().Returns(accountRepo);

        ICurrentUserService currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns((currentAccountId ?? Guid.NewGuid()).ToString());

        ITenantAccessService tenantAccessService = Substitute.For<ITenantAccessService>();

        OrganizationService service = new(unitOfWork, currentUserService, tenantAccessService);

        return new Fixture(service, organizationRepo, memberRepo, accountRepo, unitOfWork, currentUserService, tenantAccessService);
    }

    private static Account CreateAccount(string email = "member@example.com") =>
        Account.Create(new AccountParams("Member", $"member-{Guid.NewGuid():N}", email));

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesOrganizationAndAddsCallerAsOwner()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        f.OrganizationRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Organization, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Organization?)null);
        CreateOrganizationRequest request = new("Acme Inc", "acme");

        OrganizationDto result = await f.Service.CreateAsync(request, CancellationToken.None);

        Assert.Equal("Acme Inc", result.Name);
        Assert.Equal(OrganizationRole.Owner, result.MyRole);
        await f.OrganizationRepo.Received(1).AddAsync(Arg.Any<Organization>(), Arg.Any<CancellationToken>());
        await f.MemberRepo.Received(1).AddAsync(
            Arg.Is<OrganizationMember>(m => m != null && m.AccountId == accountId && m.Role == OrganizationRole.Owner),
            Arg.Any<CancellationToken>());
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
    public async Task GetMembersAsync_CallerNotAMember_ThrowsForbidden()
    {
        Fixture f = CreateFixture();
        f.MemberRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((OrganizationMember?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied,
            () => f.Service.GetMembersAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task AddMemberAsync_CallerIsPlainMember_ThrowsForbidden()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        Guid organizationId = Guid.NewGuid();
        OrganizationMember callerMembership = OrganizationMember.Create(
            new OrganizationMemberParams(organizationId, accountId, OrganizationRole.Member));
        RepositoryPredicateStub.StubFirstOrDefault(f.MemberRepo, [callerMembership]);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied,
            () => f.Service.AddMemberAsync(organizationId, new AddMemberRequest("x@example.com", OrganizationRole.Member), CancellationToken.None));
    }

    [Fact]
    public async Task AddMemberAsync_UnknownEmail_ThrowsNotFound()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        Guid organizationId = Guid.NewGuid();
        OrganizationMember callerMembership = OrganizationMember.Create(
            new OrganizationMemberParams(organizationId, accountId, OrganizationRole.Owner));
        RepositoryPredicateStub.StubFirstOrDefault(f.MemberRepo, [callerMembership]);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Account?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<NotFoundException>(
            ApplicationMessages.AccountNotFound,
            () => f.Service.AddMemberAsync(organizationId, new AddMemberRequest("nobody@example.com", OrganizationRole.Member), CancellationToken.None));
    }

    [Fact]
    public async Task AddMemberAsync_AlreadyActiveMember_ThrowsConflict()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        Guid organizationId = Guid.NewGuid();
        Account target = CreateAccount();
        OrganizationMember callerMembership = OrganizationMember.Create(
            new OrganizationMemberParams(organizationId, accountId, OrganizationRole.Owner));
        OrganizationMember targetMembership = OrganizationMember.Create(
            new OrganizationMemberParams(organizationId, target.Id, OrganizationRole.Member));
        RepositoryPredicateStub.StubFirstOrDefault(f.MemberRepo, [callerMembership, targetMembership]);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(target);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.OrganizationMemberAlreadyExists,
            () => f.Service.AddMemberAsync(organizationId, new AddMemberRequest(target.Email, OrganizationRole.Member), CancellationToken.None));
    }

    [Fact]
    public async Task AddMemberAsync_PreviouslyRemovedMember_ReactivatesWithNewRole()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        Guid organizationId = Guid.NewGuid();
        Account target = CreateAccount();
        OrganizationMember callerMembership = OrganizationMember.Create(
            new OrganizationMemberParams(organizationId, accountId, OrganizationRole.Owner));
        OrganizationMember removedMembership = OrganizationMember.Create(
            new OrganizationMemberParams(organizationId, target.Id, OrganizationRole.Member));
        removedMembership.Deactivate();
        RepositoryPredicateStub.StubFirstOrDefault(f.MemberRepo, [callerMembership, removedMembership]);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(target);

        await f.Service.AddMemberAsync(organizationId, new AddMemberRequest(target.Email, OrganizationRole.Admin), CancellationToken.None);

        Assert.True(removedMembership.IsActive);
        Assert.Equal(OrganizationRole.Admin, removedMembership.Role);
        f.MemberRepo.Received(1).Update(removedMembership);
        await f.TenantAccessService.Received(1).InvalidateMemberAsync(organizationId, target.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_DemotingLastOwner_ThrowsConflict()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        Guid organizationId = Guid.NewGuid();
        OrganizationMember onlyOwner = OrganizationMember.Create(
            new OrganizationMemberParams(organizationId, accountId, OrganizationRole.Owner));
        RepositoryPredicateStub.StubFirstOrDefault(f.MemberRepo, [onlyOwner]);
        f.MemberRepo.ListAsync(Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([onlyOwner]);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.OrganizationCannotRemoveLastOwner,
            () => f.Service.UpdateMemberRoleAsync(
                organizationId, accountId, new UpdateMemberRoleRequest(OrganizationRole.Admin), CancellationToken.None));
    }

    [Fact]
    public async Task RemoveMemberAsync_LastOwner_ThrowsConflict()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        Guid organizationId = Guid.NewGuid();
        OrganizationMember onlyOwner = OrganizationMember.Create(
            new OrganizationMemberParams(organizationId, accountId, OrganizationRole.Owner));
        RepositoryPredicateStub.StubFirstOrDefault(f.MemberRepo, [onlyOwner]);
        f.MemberRepo.ListAsync(Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([onlyOwner]);

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
        OrganizationMember owner = OrganizationMember.Create(
            new OrganizationMemberParams(organizationId, accountId, OrganizationRole.Owner));
        OrganizationMember target = OrganizationMember.Create(
            new OrganizationMemberParams(organizationId, targetAccountId, OrganizationRole.Member));
        RepositoryPredicateStub.StubFirstOrDefault(f.MemberRepo, [owner, target]);

        await f.Service.RemoveMemberAsync(organizationId, targetAccountId, CancellationToken.None);

        Assert.False(target.IsActive);
        f.MemberRepo.Received(1).Update(target);
        await f.TenantAccessService.Received(1).InvalidateMemberAsync(organizationId, targetAccountId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateAsync_CallerIsOwner_DeactivatesAndInvalidatesOrganizationCache()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        Guid organizationId = Guid.NewGuid();
        OrganizationMember owner = OrganizationMember.Create(
            new OrganizationMemberParams(organizationId, accountId, OrganizationRole.Owner));
        RepositoryPredicateStub.StubFirstOrDefault(f.MemberRepo, [owner]);
        Organization organization = Organization.Create(new OrganizationParams("Acme Inc", "acme"));
        f.OrganizationRepo.GetByIdAsync(organizationId, Arg.Any<CancellationToken>()).Returns(organization);

        await f.Service.DeactivateAsync(organizationId, CancellationToken.None);

        Assert.False(organization.Status);
        f.OrganizationRepo.Received(1).Update(organization);
        await f.TenantAccessService.Received(1).InvalidateOrganizationAsync(organizationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateAsync_CallerIsNotOwner_ThrowsForbidden()
    {
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(accountId);
        Guid organizationId = Guid.NewGuid();
        OrganizationMember admin = OrganizationMember.Create(
            new OrganizationMemberParams(organizationId, accountId, OrganizationRole.Admin));
        RepositoryPredicateStub.StubFirstOrDefault(f.MemberRepo, [admin]);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied,
            () => f.Service.DeactivateAsync(organizationId, CancellationToken.None));

        await f.OrganizationRepo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
