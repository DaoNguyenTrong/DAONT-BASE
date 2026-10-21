using Microsoft.Extensions.Options;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
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
        IPasswordHasher PasswordHasher,
        IPasswordAttemptStore PasswordAttempts);

    private static Fixture CreateFixture()
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<Account, Guid> accountRepo = Substitute.For<IRepository<Account, Guid>>();
        unitOfWork.Repository<Account, Guid>().Returns(accountRepo);

        ICurrentUserService currentUserService = Substitute.For<ICurrentUserService>();
        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();

        // First attempt of a fresh window unless a test says otherwise.
        IPasswordAttemptStore passwordAttempts = Substitute.For<IPasswordAttemptStore>();
        passwordAttempts.ReserveAttemptAsync(
                Arg.Any<PasswordAttemptPurpose>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(1);

        AccountService service = new(
            unitOfWork,
            currentUserService,
            passwordHasher,
            passwordAttempts,
            Options.Create(new LoginLockoutSettings()),
            Substitute.For<IStorageService>(),
            Options.Create(new StorageSettings()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountService>.Instance);

        return new Fixture(service, accountRepo, unitOfWork, currentUserService, passwordHasher, passwordAttempts);
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
        await f.PasswordAttempts.DidNotReceiveWithAnyArgs().ReserveAttemptAsync(default, default, default, default, default);
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

    // Reported before the check, like sign-in: refusing only a correct password would confirm the guess.
    [Fact]
    public async Task ChangePasswordAsync_Locked_RefusesWithTheLockMessage_WithoutCheckingThePassword()
    {
        Fixture f = CreateFixture();
        Account account = ArrangeAccount(f);
        f.PasswordAttempts.ReserveAttemptAsync(
                PasswordAttemptPurpose.PasswordChange, account.Id, Arg.Any<DateTime>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns((int?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.PasswordChangeTemporarilyLocked,
            () => f.Service.ChangePasswordAsync(new ChangePasswordRequest("current123", "newpassword123"), CancellationToken.None));

        f.PasswordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
        await f.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangePasswordAsync_AttemptBeyondTheLimit_IsRefusedWithoutCheckingThePassword()
    {
        Fixture f = CreateFixture();
        Account account = ArrangeAccount(f);
        f.PasswordAttempts.ReserveAttemptAsync(
                PasswordAttemptPurpose.PasswordChange, account.Id, Arg.Any<DateTime>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(6);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.PasswordChangeTemporarilyLocked,
            () => f.Service.ChangePasswordAsync(new ChangePasswordRequest("current123", "newpassword123"), CancellationToken.None));

        f.PasswordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    // Its own counter: a stolen session guessing here must not lock the owner out of signing in.
    [Fact]
    public async Task ChangePasswordAsync_WrongPasswordOnTheLastAllowedAttempt_LocksPasswordChangeOnly()
    {
        Fixture f = CreateFixture();
        Account account = ArrangeAccount(f);
        f.PasswordAttempts.ReserveAttemptAsync(
                PasswordAttemptPurpose.PasswordChange, account.Id, Arg.Any<DateTime>(), TimeSpan.FromMinutes(15), Arg.Any<CancellationToken>())
            .Returns(5);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        DateTime before = DateTime.UtcNow;
        using CancellationTokenSource aborted = new();
        await aborted.CancelAsync();

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidCurrentPassword,
            () => f.Service.ChangePasswordAsync(new ChangePasswordRequest("wrong-password", "newpassword123"), aborted.Token));

        await f.PasswordAttempts.Received(1).LockAsync(
            PasswordAttemptPurpose.PasswordChange,
            account.Id,
            Arg.Is<DateTime>(until => until >= before.AddMinutes(15) && until <= DateTime.UtcNow.AddMinutes(15)),
            Arg.Is<CancellationToken>(token => !token.CanBeCanceled));
        await f.PasswordAttempts.DidNotReceive().ReserveAttemptAsync(
            PasswordAttemptPurpose.Login, Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongPasswordBelowTheLimit_DoesNotLock()
    {
        Fixture f = CreateFixture();
        ArrangeAccount(f);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => f.Service.ChangePasswordAsync(new ChangePasswordRequest("wrong-password", "newpassword123"), CancellationToken.None));

        await f.PasswordAttempts.DidNotReceiveWithAnyArgs().LockAsync(default, default, default, default);
    }

    [Fact]
    public async Task ChangePasswordAsync_CorrectPassword_ClearsThePasswordChangeCounter()
    {
        Fixture f = CreateFixture();
        Account account = ArrangeAccount(f);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        f.PasswordHasher.Hash(Arg.Any<string>()).Returns("new-hashed-password");

        await f.Service.ChangePasswordAsync(new ChangePasswordRequest("current123", "newpassword123"), CancellationToken.None);

        await f.PasswordAttempts.Received(1).ClearAsync(
            PasswordAttemptPurpose.PasswordChange, account.Id, Arg.Any<CancellationToken>());
        await f.PasswordAttempts.DidNotReceive().ClearAsync(
            PasswordAttemptPurpose.Login, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private static Account ArrangeAccount(Fixture f)
    {
        Account account = CreateAccount();
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        return account;
    }
}
