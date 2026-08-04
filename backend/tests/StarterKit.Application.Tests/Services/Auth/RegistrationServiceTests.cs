using System.Linq.Expressions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Application.Resources;
using StarterKit.Application.Services.Auth;
using StarterKit.Application.Tests.TestSupport;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Tests.Services.Auth;

public class RegistrationServiceTests
{
    private sealed record Fixture(
        RegistrationService Service,
        IRepository<Account, Guid> AccountRepo,
        IRepository<RefreshToken, long> RefreshTokenRepo,
        IRepository<EmailVerificationToken, Guid> EmailVerificationTokenRepo,
        IUnitOfWork UnitOfWork,
        IPasswordHasher PasswordHasher,
        IEmailSender EmailSender);

    private static Fixture CreateFixture()
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<Account, Guid> accountRepo = Substitute.For<IRepository<Account, Guid>>();
        IRepository<RefreshToken, long> refreshTokenRepo = Substitute.For<IRepository<RefreshToken, long>>();
        IRepository<EmailVerificationToken, Guid> emailVerificationTokenRepo =
            Substitute.For<IRepository<EmailVerificationToken, Guid>>();
        IRepository<OrganizationMember, Guid> organizationMemberRepo = Substitute.For<IRepository<OrganizationMember, Guid>>();
        IRepository<Organization, Guid> organizationRepo = Substitute.For<IRepository<Organization, Guid>>();
        unitOfWork.Repository<Account, Guid>().Returns(accountRepo);
        unitOfWork.Repository<RefreshToken, long>().Returns(refreshTokenRepo);
        unitOfWork.Repository<EmailVerificationToken, Guid>().Returns(emailVerificationTokenRepo);
        unitOfWork.Repository<OrganizationMember, Guid>().Returns(organizationMemberRepo);
        unitOfWork.Repository<Organization, Guid>().Returns(organizationRepo);

        organizationMemberRepo.ListAsync(
                Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        IJwtTokenService jwtTokenService = Substitute.For<IJwtTokenService>();
        jwtTokenService.GenerateAccessToken(Arg.Any<Account>(), Arg.Any<Guid?>()).Returns("fake-access-token");
        jwtTokenService.GenerateRefreshToken().Returns(_ => Guid.NewGuid().ToString());

        IPermissionResolver permissionResolver = Substitute.For<IPermissionResolver>();
        permissionResolver.GetEffectivePermissionsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlySet<string>)new HashSet<string>());

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        IEmailSender emailSender = Substitute.For<IEmailSender>();

        IOptions<JwtSettings> jwtOptions = Options.Create(new JwtSettings
        {
            AccessTokenExpiryMinutes = 15,
            RefreshTokenExpiryDays = 7
        });

        IOptions<EmailSettings> emailOptions = Options.Create(new EmailSettings
        {
            FrontendBaseUrl = "https://app.example.com",
            VerificationTokenExpiryHours = 24
        });

        // Same rationale as AuthServiceTests: TokenIssuer has no I/O beyond the collaborators already
        // substituted here, so wiring the real implementation preserves VerifyEmailAsync's pre-split
        // token-issuing assertions unchanged.
        ITokenIssuer tokenIssuer = new TokenIssuer(unitOfWork, jwtTokenService, permissionResolver, jwtOptions);

        RegistrationService service = new(
            unitOfWork,
            passwordHasher,
            emailSender,
            emailOptions,
            tokenIssuer);

        return new Fixture(
            service,
            accountRepo,
            refreshTokenRepo,
            emailVerificationTokenRepo,
            unitOfWork,
            passwordHasher,
            emailSender);
    }

    private static Account CreateAccount(
        string username = "nva",
        string email = "nva@example.com",
        bool status = true,
        string? passwordHash = "hashed-password",
        bool emailConfirmed = true)
    {
        Account account = Account.Create(new AccountParams(
            Name: "Nguyen Van A",
            Username: username,
            Email: email,
            Status: status));

        if (passwordHash is not null)
        {
            account.SetPasswordHash(passwordHash);
        }

        if (emailConfirmed)
        {
            account.ConfirmEmail();
        }

        return account;
    }

    private static EmailVerificationToken CreateEmailVerificationToken(
        Guid accountId,
        string tokenHash = "hashed-token",
        DateTime? expiresAt = null)
    {
        return EmailVerificationToken.Create(new EmailVerificationTokenParams(
            accountId,
            tokenHash,
            expiresAt ?? DateTime.UtcNow.AddHours(1)));
    }

    private static RegisterRequest CreateRegisterRequest(
        string username = "nva",
        string email = "nva@example.com")
    {
        return new RegisterRequest(
            Name: "Nguyen Van A",
            Username: username,
            Email: email,
            Password: "password123");
    }

    // RegisterAsync

    [Fact]
    public async Task RegisterAsync_UsernameExists_ThrowsConflict()
    {
        Fixture f = CreateFixture();
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, [CreateAccount()]);
        RegisterRequest request = CreateRegisterRequest();

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.AccountUsernameAlreadyExists,
            () => f.Service.RegisterAsync(request, CancellationToken.None));

        await f.AccountRepo.DidNotReceive().AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_EmailExists_ThrowsConflict()
    {
        Fixture f = CreateFixture();
        RepositoryPredicateStub.StubFirstOrDefault(
            f.AccountRepo,
            [CreateAccount(username: "other", email: "nva@example.com")]);
        RegisterRequest request = CreateRegisterRequest(username: "nva", email: "nva@example.com");

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.AccountEmailAlreadyExists,
            () => f.Service.RegisterAsync(request, CancellationToken.None));

        await f.AccountRepo.DidNotReceive().AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_UniqueCredentials_CreatesUnconfirmedAccountAndSendsVerificationEmail()
    {
        Fixture f = CreateFixture();
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, []);
        f.PasswordHasher.Hash(Arg.Any<string>()).Returns("hashed-password");
        RegisterRequest request = CreateRegisterRequest();

        RegisterResult result = await f.Service.RegisterAsync(request, CancellationToken.None);

        await f.AccountRepo.Received(1).AddAsync(
            Arg.Is<Account>(a => a != null
                && a.Username == request.Username
                && a.Email == request.Email
                && a.Status
                && !a.EmailConfirmed),
            Arg.Any<CancellationToken>());
        f.PasswordHasher.Received(1).Hash(request.Password);
        await f.EmailVerificationTokenRepo.Received(1).AddAsync(
            Arg.Any<EmailVerificationToken>(), Arg.Any<CancellationToken>());
        await f.EmailSender.Received(1).SendAsync(
            request.Email, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await f.RefreshTokenRepo.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
        Assert.Equal(request.Email, result.Email);
    }

    // VerifyEmailAsync

    [Fact]
    public async Task VerifyEmailAsync_ValidToken_ConfirmsAccountAndIssuesTokens()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: false);
        const string rawToken = "raw-verification-token";
        EmailVerificationToken token = CreateEmailVerificationToken(account.Id, TokenHash.Compute(rawToken));
        RepositoryPredicateStub.StubFirstOrDefault(f.EmailVerificationTokenRepo, [token]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        VerifyEmailRequest request = new(rawToken);

        AuthResult result = await f.Service.VerifyEmailAsync(request, null, null, CancellationToken.None);

        Assert.True(account.EmailConfirmed);
        Assert.NotNull(token.ConsumedAt);
        f.AccountRepo.Received(1).Update(account);
        f.EmailVerificationTokenRepo.Received(1).Update(token);
        await f.RefreshTokenRepo.Received(1).AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
        Assert.Equal("fake-access-token", result.AccessToken);
    }

    [Fact]
    public async Task VerifyEmailAsync_TokenNotFound_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        RepositoryPredicateStub.StubFirstOrDefault(f.EmailVerificationTokenRepo, []);
        VerifyEmailRequest request = new("unknown-token");

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.EmailVerificationTokenInvalidOrExpired,
            () => f.Service.VerifyEmailAsync(request, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task VerifyEmailAsync_ExpiredToken_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: false);
        const string rawToken = "expired-token";
        EmailVerificationToken token = CreateEmailVerificationToken(
            account.Id, TokenHash.Compute(rawToken), expiresAt: DateTime.UtcNow.AddMinutes(1));
        RepositoryPredicateStub.StubFirstOrDefault(f.EmailVerificationTokenRepo, [token]);

        // Force expiry without violating the domain's "must expire in the future" invariant at creation time.
        typeof(EmailVerificationToken)
            .GetProperty(nameof(EmailVerificationToken.ExpiresAt))!
            .SetValue(token, DateTime.UtcNow.AddMinutes(-1));

        VerifyEmailRequest request = new(rawToken);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.EmailVerificationTokenInvalidOrExpired,
            () => f.Service.VerifyEmailAsync(request, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task VerifyEmailAsync_AlreadyConsumedToken_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: false);
        const string rawToken = "consumed-token";
        EmailVerificationToken token = CreateEmailVerificationToken(account.Id, TokenHash.Compute(rawToken));
        token.Consume();
        RepositoryPredicateStub.StubFirstOrDefault(f.EmailVerificationTokenRepo, [token]);
        VerifyEmailRequest request = new(rawToken);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.EmailVerificationTokenInvalidOrExpired,
            () => f.Service.VerifyEmailAsync(request, null, null, CancellationToken.None));
    }

    // ResendVerificationEmailAsync

    [Fact]
    public async Task ResendVerificationEmailAsync_AccountNotFound_ReturnsSilently()
    {
        Fixture f = CreateFixture();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Account?)null);
        ResendVerificationRequest request = new("missing@example.com");

        await f.Service.ResendVerificationEmailAsync(request, CancellationToken.None);

        await f.EmailSender.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_AlreadyConfirmed_ThrowsConflict()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: true);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        ResendVerificationRequest request = new(account.Email);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.EmailAlreadyConfirmed,
            () => f.Service.ResendVerificationEmailAsync(request, CancellationToken.None));

        await f.EmailSender.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_NotYetConfirmed_SendsNewVerificationEmail()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: false);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        ResendVerificationRequest request = new(account.Email);

        await f.Service.ResendVerificationEmailAsync(request, CancellationToken.None);

        await f.EmailVerificationTokenRepo.Received(1).AddAsync(
            Arg.Any<EmailVerificationToken>(), Arg.Any<CancellationToken>());
        await f.EmailSender.Received(1).SendAsync(
            account.Email, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
