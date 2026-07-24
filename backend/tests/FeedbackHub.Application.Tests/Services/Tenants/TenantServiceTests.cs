using System.Linq.Expressions;
using NSubstitute;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.Tenants;
using FeedbackHub.Application.Tests.TestSupport;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Tests.Services.Tenants;

public class TenantServiceTests
{
    private sealed record Fixture(
        TenantService Service,
        IRepository<Tenant, Guid> TenantRepo,
        IRepository<TenantMembership, Guid> MembershipRepo,
        IRepository<Account, Guid> AccountRepo,
        IUnitOfWork UnitOfWork,
        ICurrentUserService CurrentUserService,
        Guid CurrentAccountId);

    private static Fixture CreateFixture()
    {
        Guid currentAccountId = Guid.NewGuid();

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<Tenant, Guid> tenantRepo = Substitute.For<IRepository<Tenant, Guid>>();
        IRepository<TenantMembership, Guid> membershipRepo = Substitute.For<IRepository<TenantMembership, Guid>>();
        IRepository<Account, Guid> accountRepo = Substitute.For<IRepository<Account, Guid>>();
        unitOfWork.Repository<Tenant, Guid>().Returns(tenantRepo);
        unitOfWork.Repository<TenantMembership, Guid>().Returns(membershipRepo);
        unitOfWork.Repository<Account, Guid>().Returns(accountRepo);

        ICurrentUserService currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(currentAccountId.ToString());

        TenantService service = new(unitOfWork, currentUserService);

        return new Fixture(service, tenantRepo, membershipRepo, accountRepo, unitOfWork, currentUserService, currentAccountId);
    }

    private static Account CreateAccount(string username = "nva") =>
        Account.Create(new AccountParams(Name: "Nguyen Van A", Username: username, Email: $"{username}@example.com", Status: true));

    private static TenantMembership CreateMembership(Guid tenantId, Guid accountId, TenantRole role) =>
        TenantMembership.Create(new TenantMembershipParams(tenantId, accountId, role));

    // CreateAsync

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesTenantAndOwnerMembership()
    {
        Fixture f = CreateFixture();
        CreateTenantRequest request = new("Acme Inc", null);

        TenantDto result = await f.Service.CreateAsync(request, CancellationToken.None);

        await f.TenantRepo.Received(1).AddAsync(Arg.Is<Tenant>(t => t != null && t.Name == "Acme Inc"), Arg.Any<CancellationToken>());
        await f.MembershipRepo.Received(1).AddAsync(
            Arg.Is<TenantMembership>(m => m != null && m.AccountId == f.CurrentAccountId && m.Role == TenantRole.Owner),
            Arg.Any<CancellationToken>());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.Equal(TenantRole.Owner, result.Role);
        Assert.Equal("Acme Inc", result.Name);
    }

    // GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_NotMember_ThrowsNotFound()
    {
        Fixture f = CreateFixture();
        Guid tenantId = Guid.NewGuid();
        f.MembershipRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<TenantMembership, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((TenantMembership?)null);

        await ApplicationAssert.AssertNotFoundAsync<Tenant>(tenantId, () => f.Service.GetByIdAsync(tenantId, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_Member_ReturnsDtoWithMembershipRole()
    {
        Fixture f = CreateFixture();
        Tenant tenant = Tenant.Create(new TenantParams("Acme Inc", null));
        TenantMembership membership = CreateMembership(tenant.Id, f.CurrentAccountId, TenantRole.Member);
        f.MembershipRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<TenantMembership, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(membership);
        f.TenantRepo.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        TenantDto result = await f.Service.GetByIdAsync(tenant.Id, CancellationToken.None);

        Assert.Equal(TenantRole.Member, result.Role);
        Assert.Equal(tenant.Id, result.Id);
    }

    // GetMyTenantsAsync

    [Fact]
    public async Task GetMyTenantsAsync_NoMemberships_ReturnsEmpty()
    {
        Fixture f = CreateFixture();
        f.MembershipRepo.ListAsync(Arg.Any<Expression<Func<TenantMembership, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        IReadOnlyList<TenantDto> result = await f.Service.GetMyTenantsAsync(CancellationToken.None);

        Assert.Empty(result);
    }

    // InviteAsync

    [Fact]
    public async Task InviteAsync_CurrentNotOwner_ThrowsForbidden()
    {
        Fixture f = CreateFixture();
        Guid tenantId = Guid.NewGuid();
        RepositoryPredicateStub.StubFirstOrDefault(f.MembershipRepo, [CreateMembership(tenantId, f.CurrentAccountId, TenantRole.Member)]);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OnlyTenantOwnerCanInvite,
            () => f.Service.InviteAsync(tenantId, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task InviteAsync_TargetAccountNotFound_ThrowsNotFound()
    {
        Fixture f = CreateFixture();
        Guid tenantId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        RepositoryPredicateStub.StubFirstOrDefault(f.MembershipRepo, [CreateMembership(tenantId, f.CurrentAccountId, TenantRole.Owner)]);
        f.AccountRepo.GetByIdAsync(targetId, Arg.Any<CancellationToken>()).Returns((Account?)null);

        await ApplicationAssert.AssertNotFoundAsync<Account>(targetId, () => f.Service.InviteAsync(tenantId, targetId, CancellationToken.None));
    }

    [Fact]
    public async Task InviteAsync_AlreadyMember_ThrowsConflict()
    {
        Fixture f = CreateFixture();
        Guid tenantId = Guid.NewGuid();
        Account target = CreateAccount("target");
        RepositoryPredicateStub.StubFirstOrDefault(
            f.MembershipRepo,
            [CreateMembership(tenantId, f.CurrentAccountId, TenantRole.Owner), CreateMembership(tenantId, target.Id, TenantRole.Member)]);
        f.AccountRepo.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.AccountAlreadyMember,
            () => f.Service.InviteAsync(tenantId, target.Id, CancellationToken.None));
    }

    [Fact]
    public async Task InviteAsync_Valid_CreatesMemberMembership()
    {
        Fixture f = CreateFixture();
        Guid tenantId = Guid.NewGuid();
        Account target = CreateAccount("target");
        RepositoryPredicateStub.StubFirstOrDefault(f.MembershipRepo, [CreateMembership(tenantId, f.CurrentAccountId, TenantRole.Owner)]);
        f.AccountRepo.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);

        TenantMembershipDto result = await f.Service.InviteAsync(tenantId, target.Id, CancellationToken.None);

        Assert.Equal(TenantRole.Member, result.Role);
        await f.MembershipRepo.Received(1).AddAsync(
            Arg.Is<TenantMembership>(m => m != null && m.AccountId == target.Id && m.Role == TenantRole.Member),
            Arg.Any<CancellationToken>());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // RemoveAsync

    [Fact]
    public async Task RemoveAsync_NotOwner_ThrowsForbidden()
    {
        Fixture f = CreateFixture();
        Guid tenantId = Guid.NewGuid();
        RepositoryPredicateStub.StubFirstOrDefault(f.MembershipRepo, [CreateMembership(tenantId, f.CurrentAccountId, TenantRole.Member)]);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OnlyTenantOwnerCanRemove,
            () => f.Service.RemoveAsync(tenantId, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task RemoveAsync_RemoveSelf_ThrowsConflict()
    {
        Fixture f = CreateFixture();
        Guid tenantId = Guid.NewGuid();
        RepositoryPredicateStub.StubFirstOrDefault(f.MembershipRepo, [CreateMembership(tenantId, f.CurrentAccountId, TenantRole.Owner)]);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.CannotRemoveSelf,
            () => f.Service.RemoveAsync(tenantId, f.CurrentAccountId, CancellationToken.None));
    }

    [Fact]
    public async Task RemoveAsync_TargetIsOwner_ThrowsConflict()
    {
        Fixture f = CreateFixture();
        Guid tenantId = Guid.NewGuid();
        Guid otherOwnerId = Guid.NewGuid();
        RepositoryPredicateStub.StubFirstOrDefault(
            f.MembershipRepo,
            [CreateMembership(tenantId, f.CurrentAccountId, TenantRole.Owner), CreateMembership(tenantId, otherOwnerId, TenantRole.Owner)]);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.CannotRemoveLastOwner,
            () => f.Service.RemoveAsync(tenantId, otherOwnerId, CancellationToken.None));
    }

    [Fact]
    public async Task RemoveAsync_Valid_DeletesMembershipAndSaves()
    {
        Fixture f = CreateFixture();
        Guid tenantId = Guid.NewGuid();
        TenantMembership target = CreateMembership(tenantId, Guid.NewGuid(), TenantRole.Member);
        RepositoryPredicateStub.StubFirstOrDefault(
            f.MembershipRepo,
            [CreateMembership(tenantId, f.CurrentAccountId, TenantRole.Owner), target]);

        await f.Service.RemoveAsync(tenantId, target.AccountId, CancellationToken.None);

        f.MembershipRepo.Received(1).Delete(target);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // TransferOwnershipAsync

    [Fact]
    public async Task TransferOwnershipAsync_NotOwner_ThrowsForbidden()
    {
        Fixture f = CreateFixture();
        Guid tenantId = Guid.NewGuid();
        RepositoryPredicateStub.StubFirstOrDefault(f.MembershipRepo, [CreateMembership(tenantId, f.CurrentAccountId, TenantRole.Member)]);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OnlyTenantOwnerCanTransfer,
            () => f.Service.TransferOwnershipAsync(tenantId, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task TransferOwnershipAsync_ToSelf_ThrowsConflict()
    {
        Fixture f = CreateFixture();
        Guid tenantId = Guid.NewGuid();
        RepositoryPredicateStub.StubFirstOrDefault(f.MembershipRepo, [CreateMembership(tenantId, f.CurrentAccountId, TenantRole.Owner)]);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.CannotTransferToSelf,
            () => f.Service.TransferOwnershipAsync(tenantId, f.CurrentAccountId, CancellationToken.None));
    }

    [Fact]
    public async Task TransferOwnershipAsync_NewOwnerNotMember_ThrowsNotFound()
    {
        Fixture f = CreateFixture();
        Guid tenantId = Guid.NewGuid();
        Guid newOwnerId = Guid.NewGuid();
        RepositoryPredicateStub.StubFirstOrDefault(f.MembershipRepo, [CreateMembership(tenantId, f.CurrentAccountId, TenantRole.Owner)]);

        await ApplicationAssert.AssertNotFoundAsync<Account>(
            newOwnerId,
            () => f.Service.TransferOwnershipAsync(tenantId, newOwnerId, CancellationToken.None));
    }

    // Regression guard for the TenantRole.Owner == default(TenantRole) bug: promoting the new
    // owner calls TenantMembership.Update(..., TenantRole.Owner), which must succeed.
    [Fact]
    public async Task TransferOwnershipAsync_Valid_DemotesCurrentAndPromotesNewOwner()
    {
        Fixture f = CreateFixture();
        Guid tenantId = Guid.NewGuid();
        Account newOwnerAccount = CreateAccount("newowner");
        TenantMembership newOwnerMembership = CreateMembership(tenantId, newOwnerAccount.Id, TenantRole.Member);
        RepositoryPredicateStub.StubFirstOrDefault(
            f.MembershipRepo,
            [CreateMembership(tenantId, f.CurrentAccountId, TenantRole.Owner), newOwnerMembership]);
        f.AccountRepo.GetByIdAsync(newOwnerAccount.Id, Arg.Any<CancellationToken>()).Returns(newOwnerAccount);

        TenantMembershipDto result = await f.Service.TransferOwnershipAsync(tenantId, newOwnerAccount.Id, CancellationToken.None);

        Assert.Equal(TenantRole.Owner, result.Role);
        Assert.Equal(TenantRole.Owner, newOwnerMembership.Role);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ListMembersAsync

    [Fact]
    public async Task ListMembersAsync_NotMember_ReturnsEmpty()
    {
        Fixture f = CreateFixture();
        Guid tenantId = Guid.NewGuid();
        f.MembershipRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<TenantMembership, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((TenantMembership?)null);

        IReadOnlyList<TenantMembershipDto> result = await f.Service.ListMembersAsync(tenantId, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListMembersAsync_Member_ReturnsAllMembersWithUsernames()
    {
        Fixture f = CreateFixture();
        Guid tenantId = Guid.NewGuid();
        Account other = CreateAccount("other");
        TenantMembership ownerMembership = CreateMembership(tenantId, f.CurrentAccountId, TenantRole.Owner);
        TenantMembership otherMembership = CreateMembership(tenantId, other.Id, TenantRole.Member);
        f.MembershipRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<TenantMembership, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(ownerMembership);
        f.MembershipRepo.ListAsync(Arg.Any<Expression<Func<TenantMembership, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([ownerMembership, otherMembership]);
        f.AccountRepo.ListAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([other]);

        IReadOnlyList<TenantMembershipDto> result = await f.Service.ListMembersAsync(tenantId, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(other.Id, result[0].AccountId);
        Assert.Equal(TenantRole.Member, result[0].Role);
    }
}
