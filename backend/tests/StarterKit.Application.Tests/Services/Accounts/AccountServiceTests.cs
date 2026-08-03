using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Resources;
using StarterKit.Application.Services.Accounts;
using StarterKit.Application.Tests.TestSupport;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Tests.Services.Accounts;

public class AccountServiceTests
{
    private sealed record Fixture(
        AccountService Service,
        IRepository<Account, Guid> AccountRepo,
        IUnitOfWork UnitOfWork,
        ICurrentUserService CurrentUserService,
        IPasswordHasher PasswordHasher);

    private static Fixture CreateFixture()
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<Account, Guid> accountRepo = Substitute.For<IRepository<Account, Guid>>();
        unitOfWork.Repository<Account, Guid>().Returns(accountRepo);

        ICurrentUserService currentUserService = Substitute.For<ICurrentUserService>();
        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();

        AccountService service = new(unitOfWork, currentUserService, passwordHasher);

        return new Fixture(service, accountRepo, unitOfWork, currentUserService, passwordHasher);
    }

    private static Account CreateAccount(
        string name = "Nguyen Van A",
        string username = "nva",
        string email = "nva@example.com",
        string? passwordHash = "hashed-password")
    {
        Account account = Account.Create(new AccountParams(name, username, email));

        if (passwordHash is not null)
        {
            account.SetPasswordHash(passwordHash);
        }

        return account;
    }

    // GetCurrentProfileAsync

    [Fact]
    public async Task GetCurrentProfileAsync_UnparsableUserId_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        f.CurrentUserService.UserId.Returns((string?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.AuthenticatedUserRequired,
            () => f.Service.GetCurrentProfileAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetCurrentProfileAsync_AccountMissing_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        Guid id = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(id.ToString());
        f.AccountRepo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Account?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.AuthenticatedUserRequired,
            () => f.Service.GetCurrentProfileAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetCurrentProfileAsync_Valid_ReturnsProfileDto()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        ProfileDto dto = await f.Service.GetCurrentProfileAsync(CancellationToken.None);

        Assert.Equal(account.Id, dto.Id);
        Assert.True(dto.HasPassword);
    }

    // UpdateCurrentProfileAsync

    [Fact]
    public async Task UpdateCurrentProfileAsync_EmailCollisionWithOtherAccount_ThrowsConflict()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        Account other = CreateAccount(username: "other", email: "taken@example.com");
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, [account, other]);
        UpdateProfileRequest request = new("Name", null, null, null, "taken@example.com");

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.AccountEmailAlreadyExists,
            () => f.Service.UpdateCurrentProfileAsync(request, CancellationToken.None));

        f.AccountRepo.DidNotReceive().Update(Arg.Any<Account>());
    }

    [Fact]
    public async Task UpdateCurrentProfileAsync_SameEmailAsOwnAccount_DoesNotThrow()
    {
        // Guards the predicate's self-exclusion clause (candidate.Id != account.Id) — without it,
        // a user keeping their own unchanged email would incorrectly collide with themselves.
        Fixture f = CreateFixture();
        Account account = CreateAccount(email: "nva@example.com");
        Account other = CreateAccount(username: "other", email: "taken@example.com");
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, [account, other]);
        UpdateProfileRequest request = new("New Name", null, null, null, "nva@example.com");

        ProfileDto dto = await f.Service.UpdateCurrentProfileAsync(request, CancellationToken.None);

        Assert.Equal("New Name", dto.Name);
        Assert.Equal("nva@example.com", dto.Email);
        f.AccountRepo.Received(1).Update(account);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateCurrentProfileAsync_Success_PreservesUsernameAndStatus()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(username: "keep-username");
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, [account]);
        UpdateProfileRequest request = new("New Name", "0123456789", "Dev", "Address", "new@example.com");

        ProfileDto dto = await f.Service.UpdateCurrentProfileAsync(request, CancellationToken.None);

        Assert.Equal("keep-username", dto.Username);
        Assert.Equal("New Name", dto.Name);
        Assert.Equal("new@example.com", dto.Email);
        Assert.True(account.Status);
        f.AccountRepo.Received(1).Update(account);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ChangePasswordAsync

    [Fact]
    public async Task ChangePasswordAsync_NoPasswordHash_ThrowsUnauthorized_AndDoesNotCallVerify()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(passwordHash: null);
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        ChangePasswordRequest request = new("current123", "newpassword123");

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidCurrentPassword,
            () => f.Service.ChangePasswordAsync(request, CancellationToken.None));

        f.PasswordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        ChangePasswordRequest request = new("wrong-password", "newpassword123");

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidCurrentPassword,
            () => f.Service.ChangePasswordAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task ChangePasswordAsync_Success_HashesAndSavesNewPassword()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        f.PasswordHasher.Hash("newpassword123").Returns("new-hashed-password");
        ChangePasswordRequest request = new("current123", "newpassword123");

        await f.Service.ChangePasswordAsync(request, CancellationToken.None);

        Assert.Equal("new-hashed-password", account.PasswordHash);
        f.AccountRepo.Received(1).Update(account);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
