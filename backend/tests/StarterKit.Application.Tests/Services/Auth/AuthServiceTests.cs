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

public class AuthServiceTests
{
    private const string GoogleProviderName = "Google";

    private sealed record Fixture(
        AuthService Service,
        IRepository<Account, Guid> AccountRepo,
        IRepository<RefreshToken, long> RefreshTokenRepo,
        IRepository<EmailVerificationToken, Guid> EmailVerificationTokenRepo,
        IRepository<ExternalLogin, Guid> ExternalLoginRepo,
        IUnitOfWork UnitOfWork,
        IJwtTokenService JwtTokenService,
        ITenantAccessService TenantAccessService,
        IPermissionResolver PermissionResolver,
        IPasswordHasher PasswordHasher,
        IEmailSender EmailSender,
        IExternalAuthProvider ExternalAuthProvider,
        ICurrentUserService CurrentUserService,
        IRepository<OrganizationMember, Guid> OrganizationMemberRepo,
        IRepository<Organization, Guid> OrganizationRepo);

    private static Fixture CreateFixture(
        int accessTokenExpiryMinutes = 15,
        int refreshTokenExpiryDays = 7)
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<Account, Guid> accountRepo = Substitute.For<IRepository<Account, Guid>>();
        IRepository<RefreshToken, long> refreshTokenRepo = Substitute.For<IRepository<RefreshToken, long>>();
        IRepository<EmailVerificationToken, Guid> emailVerificationTokenRepo =
            Substitute.For<IRepository<EmailVerificationToken, Guid>>();
        IRepository<ExternalLogin, Guid> externalLoginRepo = Substitute.For<IRepository<ExternalLogin, Guid>>();
        IRepository<OrganizationMember, Guid> organizationMemberRepo = Substitute.For<IRepository<OrganizationMember, Guid>>();
        IRepository<Organization, Guid> organizationRepo = Substitute.For<IRepository<Organization, Guid>>();
        unitOfWork.Repository<Account, Guid>().Returns(accountRepo);
        unitOfWork.Repository<RefreshToken, long>().Returns(refreshTokenRepo);
        unitOfWork.Repository<EmailVerificationToken, Guid>().Returns(emailVerificationTokenRepo);
        unitOfWork.Repository<ExternalLogin, Guid>().Returns(externalLoginRepo);
        unitOfWork.Repository<OrganizationMember, Guid>().Returns(organizationMemberRepo);
        unitOfWork.Repository<Organization, Guid>().Returns(organizationRepo);

        // Default: account belongs to no organization, matching pre-multi-tenant behavior
        // (tokens issued with no org claim) unless a test explicitly seeds memberships.
        organizationMemberRepo.ListAsync(
                Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        IJwtTokenService jwtTokenService = Substitute.For<IJwtTokenService>();
        jwtTokenService.GenerateAccessToken(Arg.Any<Account>(), Arg.Any<Guid?>()).Returns("fake-access-token");
        jwtTokenService.GenerateRefreshToken().Returns(_ => Guid.NewGuid().ToString());

        ITenantAccessService tenantAccessService = Substitute.For<ITenantAccessService>();
        tenantAccessService.HasActiveAccessAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

        IPermissionResolver permissionResolver = Substitute.For<IPermissionResolver>();
        permissionResolver.GetEffectivePermissionsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlySet<string>)new HashSet<string>());

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        IEmailSender emailSender = Substitute.For<IEmailSender>();

        IExternalAuthProvider externalAuthProvider = Substitute.For<IExternalAuthProvider>();
        externalAuthProvider.ProviderName.Returns(GoogleProviderName);

        ICurrentUserService currentUserService = Substitute.For<ICurrentUserService>();

        IOptions<JwtSettings> jwtOptions = Options.Create(new JwtSettings
        {
            AccessTokenExpiryMinutes = accessTokenExpiryMinutes,
            RefreshTokenExpiryDays = refreshTokenExpiryDays
        });

        IOptions<EmailSettings> emailOptions = Options.Create(new EmailSettings
        {
            FrontendBaseUrl = "https://app.example.com",
            VerificationTokenExpiryHours = 24
        });

        AuthService service = new(
            unitOfWork,
            currentUserService,
            jwtTokenService,
            tenantAccessService,
            permissionResolver,
            passwordHasher,
            emailSender,
            [externalAuthProvider],
            jwtOptions,
            emailOptions);

        return new Fixture(
            service,
            accountRepo,
            refreshTokenRepo,
            emailVerificationTokenRepo,
            externalLoginRepo,
            unitOfWork,
            jwtTokenService,
            tenantAccessService,
            permissionResolver,
            passwordHasher,
            emailSender,
            externalAuthProvider,
            currentUserService,
            organizationMemberRepo,
            organizationRepo);
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

    private static RefreshToken CreateRefreshToken(
        Guid accountId,
        string rawToken,
        bool isPersistent = false,
        DateTime? expiresAt = null,
        DateTime? loginAt = null)
    {
        return RefreshToken.Create(new RefreshTokenParams(
            accountId,
            ComputeSha256(rawToken),
            expiresAt ?? DateTime.UtcNow.AddDays(1),
            DeviceInfo: null,
            IpAddress: null,
            IsPersistent: isPersistent,
            LoginAt: loginAt ?? DateTime.UtcNow));
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

    // LoginAsync

    [Fact]
    public async Task LoginAsync_UsernameNotFound_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Account?)null);
        LoginRequest request = new("nva", "password123");

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidUsernameOrPassword,
            () => f.Service.LoginAsync(request, null, null, CancellationToken.None));

        f.PasswordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task LoginAsync_AccountInactive_ThrowsUnauthorized_AndDoesNotCallVerify()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(status: false);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        LoginRequest request = new(account.Username, "password123");

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidUsernameOrPassword,
            () => f.Service.LoginAsync(request, null, null, CancellationToken.None));

        f.PasswordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task LoginAsync_PasswordHashNull_ThrowsUnauthorized_AndDoesNotCallVerify()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(passwordHash: null);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        LoginRequest request = new(account.Username, "password123");

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidUsernameOrPassword,
            () => f.Service.LoginAsync(request, null, null, CancellationToken.None));

        f.PasswordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task LoginAsync_PasswordMismatch_ThrowsUnauthorized_AndCallsVerifyOnce()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        LoginRequest request = new(account.Username, "wrong-password");

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidUsernameOrPassword,
            () => f.Service.LoginAsync(request, null, null, CancellationToken.None));

        f.PasswordHasher.Received(1).Verify(request.Password, account.PasswordHash!);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsAuthResult_AndPersistsRefreshToken()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        LoginRequest request = new(account.Username, "correct-password");

        AuthResult result = await f.Service.LoginAsync(request, null, null, CancellationToken.None);

        Assert.Equal("fake-access-token", result.AccessToken);
        Assert.Equal(account.Id, result.Account.Id);
        await f.RefreshTokenRepo.Received(1).AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        double diffSeconds = Math.Abs((result.AccessTokenExpiry - DateTime.UtcNow.AddMinutes(15)).TotalSeconds);
        Assert.True(diffSeconds < 5, $"Expected access token expiry within 5s tolerance, was off by {diffSeconds}s");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoginAsync_KeepLoggedIn_PassesIsPersistentToIssuedToken(bool keepLoggedIn)
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        LoginRequest request = new(account.Username, "correct-password", keepLoggedIn);

        AuthResult result = await f.Service.LoginAsync(request, null, null, CancellationToken.None);

        Assert.Equal(keepLoggedIn, result.IsPersistent);
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

    // LoginAsync — email confirmation gate

    [Fact]
    public async Task LoginAsync_EmailNotConfirmed_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: false);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        LoginRequest request = new(account.Username, "correct-password");

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.EmailNotConfirmed,
            () => f.Service.LoginAsync(request, null, null, CancellationToken.None));

        await f.RefreshTokenRepo.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    // VerifyEmailAsync

    [Fact]
    public async Task VerifyEmailAsync_ValidToken_ConfirmsAccountAndIssuesTokens()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: false);
        const string rawToken = "raw-verification-token";
        EmailVerificationToken token = CreateEmailVerificationToken(account.Id, ComputeSha256(rawToken));
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
            account.Id, ComputeSha256(rawToken), expiresAt: DateTime.UtcNow.AddMinutes(1));
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
        EmailVerificationToken token = CreateEmailVerificationToken(account.Id, ComputeSha256(rawToken));
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

    private static string ComputeSha256(string input)
    {
        byte[] bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    // ExternalLoginAsync

    [Fact]
    public async Task ExternalLoginAsync_UnsupportedProvider_ThrowsDomainException()
    {
        Fixture f = CreateFixture();
        ExternalLoginRequest request = new("some-credential");

        await ApplicationAssert.ThrowsWithMessageAsync<DomainException>(
            ApplicationMessages.ExternalLoginProviderNotSupported,
            () => f.Service.ExternalLoginAsync("github", request, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task ExternalLoginAsync_ProviderEmailNotVerified_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalUserInfo("google-sub-unverified", "attacker@example.com", "Attacker", EmailVerified: false));
        ExternalLoginRequest request = new("some-credential");

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.ExternalLoginEmailNotVerifiedByProvider,
            () => f.Service.ExternalLoginAsync("google", request, null, null, CancellationToken.None));

        await f.ExternalLoginRepo.DidNotReceive().FirstOrDefaultAsync(
            Arg.Any<Expression<Func<ExternalLogin, bool>>>(), Arg.Any<CancellationToken>());
        await f.AccountRepo.DidNotReceive().AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExternalLoginAsync_ExistingLink_LogsInWithoutCreatingAccountOrLink()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        ExternalLogin link = ExternalLogin.Create(new ExternalLoginParams(
            account.Id, GoogleProviderName, "google-sub-1", account.Email));
        RepositoryPredicateStub.StubFirstOrDefault(f.ExternalLoginRepo, [link]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalUserInfo("google-sub-1", account.Email, "Nguyen Van A", EmailVerified: true));
        ExternalLoginRequest request = new("some-credential");

        AuthResult result = await f.Service.ExternalLoginAsync("google", request, null, null, CancellationToken.None);

        await f.AccountRepo.DidNotReceive().AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
        await f.ExternalLoginRepo.DidNotReceive().AddAsync(Arg.Any<ExternalLogin>(), Arg.Any<CancellationToken>());
        Assert.Equal("fake-access-token", result.AccessToken);
    }

    [Fact]
    public async Task ExternalLoginAsync_NoMatchingEmail_CreatesConfirmedAccountAndLinksProvider()
    {
        Fixture f = CreateFixture();
        RepositoryPredicateStub.StubFirstOrDefault(f.ExternalLoginRepo, []);
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, []);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalUserInfo("google-sub-2", "newuser@example.com", "New User", EmailVerified: true));
        ExternalLoginRequest request = new("some-credential");

        AuthResult result = await f.Service.ExternalLoginAsync("google", request, null, null, CancellationToken.None);

        await f.AccountRepo.Received(1).AddAsync(
            Arg.Is<Account>(a => a != null && a.Email == "newuser@example.com" && a.Username == "newuser" && a.EmailConfirmed),
            Arg.Any<CancellationToken>());
        await f.ExternalLoginRepo.Received(1).AddAsync(
            Arg.Is<ExternalLogin>(l => l != null && l.Provider == GoogleProviderName && l.ProviderUserId == "google-sub-2"),
            Arg.Any<CancellationToken>());
        Assert.Equal("fake-access-token", result.AccessToken);
    }

    [Fact]
    public async Task ExternalLoginAsync_UsernameCollision_AppendsSuffix()
    {
        Fixture f = CreateFixture();
        RepositoryPredicateStub.StubFirstOrDefault(f.ExternalLoginRepo, []);
        RepositoryPredicateStub.StubFirstOrDefault(
            f.AccountRepo, [CreateAccount(username: "newuser", email: "someone-else@example.com")]);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalUserInfo("google-sub-3", "newuser@example.com", "New User", EmailVerified: true));
        ExternalLoginRequest request = new("some-credential");

        await f.Service.ExternalLoginAsync("google", request, null, null, CancellationToken.None);

        await f.AccountRepo.Received(1).AddAsync(
            Arg.Is<Account>(a => a != null && a.Username == "newuser1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExternalLoginAsync_EmailMatchesUnconfirmedAccount_ThrowsConflict()
    {
        Fixture f = CreateFixture();
        Account existing = CreateAccount(email: "pending@example.com", emailConfirmed: false);
        RepositoryPredicateStub.StubFirstOrDefault(f.ExternalLoginRepo, []);
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, [existing]);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalUserInfo("google-sub-4", "pending@example.com", "Pending User", EmailVerified: true));
        ExternalLoginRequest request = new("some-credential");

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.ExternalLoginEmailNotConfirmed,
            () => f.Service.ExternalLoginAsync("google", request, null, null, CancellationToken.None));

        await f.AccountRepo.DidNotReceive().AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
        await f.ExternalLoginRepo.DidNotReceive().AddAsync(Arg.Any<ExternalLogin>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExternalLoginAsync_EmailMatchesConfirmedAccount_AutoLinksWithoutCreatingNewAccount()
    {
        Fixture f = CreateFixture();
        Account existing = CreateAccount(email: "verified@example.com", emailConfirmed: true);
        RepositoryPredicateStub.StubFirstOrDefault(f.ExternalLoginRepo, []);
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, [existing]);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalUserInfo("google-sub-5", "verified@example.com", "Verified User", EmailVerified: true));
        ExternalLoginRequest request = new("some-credential");

        AuthResult result = await f.Service.ExternalLoginAsync("google", request, null, null, CancellationToken.None);

        await f.AccountRepo.DidNotReceive().AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
        await f.ExternalLoginRepo.Received(1).AddAsync(
            Arg.Is<ExternalLogin>(l => l != null && l.AccountId == existing.Id && l.ProviderUserId == "google-sub-5"),
            Arg.Any<CancellationToken>());
        Assert.Equal(existing.Id, result.Account.Id);
    }

    // RefreshTokenAsync

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RefreshTokenAsync_BlankToken_ThrowsUnauthorized_ZeroRepoCalls(string? token)
    {
        Fixture f = CreateFixture();

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.RefreshTokenRequired,
            () => f.Service.RefreshTokenAsync(token!, null, null, CancellationToken.None));

        await f.RefreshTokenRepo.DidNotReceive().FirstOrDefaultAsync(Arg.Any<Expression<Func<RefreshToken, bool>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshTokenAsync_TokenNotFound_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        f.RefreshTokenRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<RefreshToken, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidRefreshToken,
            () => f.Service.RefreshTokenAsync("some-token", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task RefreshTokenAsync_TokenRevoked_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        RefreshToken token = CreateRefreshToken(Guid.NewGuid(), "some-token");
        token.Revoke();
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [token]);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidRefreshToken,
            () => f.Service.RefreshTokenAsync("some-token", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task RefreshTokenAsync_AccountNotFound_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        RefreshToken token = CreateRefreshToken(Guid.NewGuid(), "some-token");
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [token]);
        f.AccountRepo.GetByIdAsync(token.AccountId, Arg.Any<CancellationToken>()).Returns((Account?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidRefreshToken,
            () => f.Service.RefreshTokenAsync("some-token", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task RefreshTokenAsync_AccountInactive_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(status: false);
        RefreshToken token = CreateRefreshToken(account.Id, "some-token");
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [token]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidRefreshToken,
            () => f.Service.RefreshTokenAsync("some-token", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_RevokesOldToken_AndIssuesNewOne()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        RefreshToken oldToken = CreateRefreshToken(account.Id, "old-token");
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [oldToken]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        AuthResult result = await f.Service.RefreshTokenAsync("old-token", null, null, CancellationToken.None);

        Assert.NotNull(oldToken.RevokedAt);
        f.RefreshTokenRepo.Received(1).Update(oldToken);
        Assert.NotEqual("old-token", result.RefreshToken);
        await f.RefreshTokenRepo.Received(1).AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RefreshTokenAsync_ValidToken_CarriesOldIsPersistentForward(bool oldIsPersistent)
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        RefreshToken oldToken = CreateRefreshToken(account.Id, "old-token", isPersistent: oldIsPersistent);
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [oldToken]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        AuthResult result = await f.Service.RefreshTokenAsync("old-token", null, null, CancellationToken.None);

        Assert.Equal(oldIsPersistent, result.IsPersistent);
    }

    // RevokeTokenAsync

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RevokeTokenAsync_BlankToken_ReturnsSilently_NoRepoCalls(string? token)
    {
        Fixture f = CreateFixture();

        await f.Service.RevokeTokenAsync(token!, CancellationToken.None);

        await f.RefreshTokenRepo.DidNotReceive().FirstOrDefaultAsync(Arg.Any<Expression<Func<RefreshToken, bool>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeTokenAsync_TokenNotFound_ReturnsSilently_NoSaveChanges()
    {
        Fixture f = CreateFixture();
        f.RefreshTokenRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<RefreshToken, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        await f.Service.RevokeTokenAsync("some-token", CancellationToken.None);

        await f.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeTokenAsync_TokenFound_RevokesAndSaves()
    {
        Fixture f = CreateFixture();
        RefreshToken token = CreateRefreshToken(Guid.NewGuid(), "some-token");
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [token]);

        await f.Service.RevokeTokenAsync("some-token", CancellationToken.None);

        Assert.NotNull(token.RevokedAt);
        f.RefreshTokenRepo.Received(1).Update(token);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // GetSessionsAsync

    [Fact]
    public async Task GetSessionsAsync_ExcludesRevokedTokens_MarksCurrentAndOrdersNewestFirst()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());

        RefreshToken older = CreateRefreshToken(accountId, "older-token");
        older.Id = 1;
        older.CreatedAt = DateTime.UtcNow.AddHours(-1);

        RefreshToken newer = CreateRefreshToken(accountId, "newer-token");
        newer.Id = 2;
        newer.CreatedAt = DateTime.UtcNow;

        RefreshToken revoked = CreateRefreshToken(accountId, "revoked-token");
        revoked.Id = 3;
        revoked.Revoke();

        f.RefreshTokenRepo.ListAsync(Arg.Any<Expression<Func<RefreshToken, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([older, newer, revoked]);

        IReadOnlyList<SessionDto> sessions = await f.Service.GetSessionsAsync("newer-token", CancellationToken.None);

        Assert.Equal(2, sessions.Count);
        Assert.Equal(newer.Id, sessions[0].Id);
        Assert.True(sessions[0].IsCurrent);
        Assert.Equal(older.Id, sessions[1].Id);
        Assert.False(sessions[1].IsCurrent);
    }

    [Fact]
    public async Task GetSessionsAsync_NotAuthenticated_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        f.CurrentUserService.UserId.Returns((string?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.AuthenticatedUserRequired,
            () => f.Service.GetSessionsAsync(null, CancellationToken.None));
    }

    // RevokeSessionAsync

    [Fact]
    public async Task RevokeSessionAsync_TokenNotFound_ThrowsNotFound()
    {
        Fixture f = CreateFixture();
        f.CurrentUserService.UserId.Returns(Guid.NewGuid().ToString());
        f.RefreshTokenRepo.GetByIdAsync(42, Arg.Any<CancellationToken>()).Returns((RefreshToken?)null);

        await ApplicationAssert.AssertNotFoundAsync<RefreshToken>(
            42L,
            () => f.Service.RevokeSessionAsync(42, CancellationToken.None));
    }

    [Fact]
    public async Task RevokeSessionAsync_TokenNotOwnedByCurrentAccount_ThrowsNotFound()
    {
        Fixture f = CreateFixture();
        f.CurrentUserService.UserId.Returns(Guid.NewGuid().ToString());
        RefreshToken othersToken = CreateRefreshToken(Guid.NewGuid(), "some-token");
        othersToken.Id = 42;
        f.RefreshTokenRepo.GetByIdAsync(42, Arg.Any<CancellationToken>()).Returns(othersToken);

        await ApplicationAssert.AssertNotFoundAsync<RefreshToken>(
            42L,
            () => f.Service.RevokeSessionAsync(42, CancellationToken.None));
    }

    [Fact]
    public async Task RevokeSessionAsync_OwnedByCurrentAccount_RevokesAndSaves()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());
        RefreshToken token = CreateRefreshToken(accountId, "some-token");
        token.Id = 7;
        f.RefreshTokenRepo.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(token);

        await f.Service.RevokeSessionAsync(7, CancellationToken.None);

        Assert.NotNull(token.RevokedAt);
        f.RefreshTokenRepo.Received(1).Update(token);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // RevokeOtherSessionsAsync

    [Fact]
    public async Task RevokeOtherSessionsAsync_RevokesEveryTokenExceptCurrent()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());

        RefreshToken current = CreateRefreshToken(accountId, "current-token");
        RefreshToken other1 = CreateRefreshToken(accountId, "other-token-1");
        RefreshToken other2 = CreateRefreshToken(accountId, "other-token-2");
        RefreshToken alreadyRevoked = CreateRefreshToken(accountId, "already-revoked");
        alreadyRevoked.Revoke();

        f.RefreshTokenRepo.ListAsync(Arg.Any<Expression<Func<RefreshToken, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([current, other1, other2, alreadyRevoked]);

        await f.Service.RevokeOtherSessionsAsync("current-token", CancellationToken.None);

        Assert.Null(current.RevokedAt);
        Assert.NotNull(other1.RevokedAt);
        Assert.NotNull(other2.RevokedAt);
        f.RefreshTokenRepo.DidNotReceive().Update(current);
        f.RefreshTokenRepo.Received(1).Update(other1);
        f.RefreshTokenRepo.Received(1).Update(other2);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeOtherSessionsAsync_NoCurrentTokenIdentified_RevokesAllActiveSessions()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());

        RefreshToken token1 = CreateRefreshToken(accountId, "token-1");
        RefreshToken token2 = CreateRefreshToken(accountId, "token-2");

        f.RefreshTokenRepo.ListAsync(Arg.Any<Expression<Func<RefreshToken, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([token1, token2]);

        await f.Service.RevokeOtherSessionsAsync(null, CancellationToken.None);

        Assert.NotNull(token1.RevokedAt);
        Assert.NotNull(token2.RevokedAt);
    }

    // Organization scoping

    [Fact]
    public async Task LoginAsync_AccountBelongsToExactlyOneOrganization_IssuesTokenScopedToThatOrganization()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        Organization organization = Organization.Create(new OrganizationParams("Acme", "acme"));
        OrganizationMember membership = OrganizationMember.Create(
            new OrganizationMemberParams(organization.Id, account.Id));
        f.OrganizationMemberRepo.ListAsync(
                Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([membership]);
        f.OrganizationRepo.GetByIdAsync(organization.Id, Arg.Any<CancellationToken>()).Returns(organization);
        LoginRequest request = new(account.Username, "correct-password");

        AuthResult result = await f.Service.LoginAsync(request, null, null, CancellationToken.None);

        Assert.Equal(organization.Id, result.OrganizationId);
        Assert.Equal(organization.Name, result.OrganizationName);
        f.JwtTokenService.Received(1).GenerateAccessToken(account, organization.Id);
    }

    [Fact]
    public async Task LoginAsync_AccountBelongsToMultipleOrganizations_IssuesTokenWithNoOrganization()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        f.OrganizationMemberRepo.ListAsync(
                Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([
                OrganizationMember.Create(new OrganizationMemberParams(Guid.NewGuid(), account.Id)),
                OrganizationMember.Create(new OrganizationMemberParams(Guid.NewGuid(), account.Id))
            ]);
        LoginRequest request = new(account.Username, "correct-password");

        AuthResult result = await f.Service.LoginAsync(request, null, null, CancellationToken.None);

        Assert.Null(result.OrganizationId);
        f.JwtTokenService.Received(1).GenerateAccessToken(account, null);
    }

    [Fact]
    public async Task RefreshTokenAsync_TokenHasOrganization_PreservesOrganizationOnNewToken()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        Guid organizationId = Guid.NewGuid();
        RefreshToken oldToken = RefreshToken.Create(new RefreshTokenParams(
            account.Id, ComputeSha256("old-token"), DateTime.UtcNow.AddDays(1), null, null, false, DateTime.UtcNow, organizationId));
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [oldToken]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        f.OrganizationRepo.GetByIdAsync(organizationId, Arg.Any<CancellationToken>())
            .Returns(Organization.Create(new OrganizationParams("Acme", "acme")));

        AuthResult result = await f.Service.RefreshTokenAsync("old-token", null, null, CancellationToken.None);

        Assert.Equal(organizationId, result.OrganizationId);
        f.JwtTokenService.Received(1).GenerateAccessToken(account, organizationId);
    }

    [Fact]
    public async Task RefreshTokenAsync_TokenHasOrganization_AccessRevoked_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        Guid organizationId = Guid.NewGuid();
        RefreshToken oldToken = RefreshToken.Create(new RefreshTokenParams(
            account.Id, ComputeSha256("old-token"), DateTime.UtcNow.AddDays(1), null, null, false, DateTime.UtcNow, organizationId));
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [oldToken]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        f.TenantAccessService.HasActiveAccessAsync(account.Id, organizationId, Arg.Any<CancellationToken>())
            .Returns(false);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidRefreshToken,
            () => f.Service.RefreshTokenAsync("old-token", null, null, CancellationToken.None));
    }

    // SwitchOrganizationAsync

    [Fact]
    public async Task SwitchOrganizationAsync_NotAMember_ThrowsForbidden()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());
        f.OrganizationMemberRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((OrganizationMember?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied,
            () => f.Service.SwitchOrganizationAsync(Guid.NewGuid(), null, null, CancellationToken.None));
    }

    [Fact]
    public async Task SwitchOrganizationAsync_OrganizationInactive_ThrowsForbidden()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());

        Organization organization = Organization.Create(new OrganizationParams("Acme", "acme"));
        organization.Deactivate();
        OrganizationMember membership = OrganizationMember.Create(
            new OrganizationMemberParams(organization.Id, accountId));
        RepositoryPredicateStub.StubFirstOrDefault(f.OrganizationMemberRepo, [membership]);
        f.OrganizationRepo.GetByIdAsync(organization.Id, Arg.Any<CancellationToken>()).Returns(organization);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied,
            () => f.Service.SwitchOrganizationAsync(organization.Id, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task SwitchOrganizationAsync_ActiveMember_IssuesTokenScopedToOrganization()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        Organization organization = Organization.Create(new OrganizationParams("Acme", "acme"));
        OrganizationMember membership = OrganizationMember.Create(
            new OrganizationMemberParams(organization.Id, account.Id));
        RepositoryPredicateStub.StubFirstOrDefault(f.OrganizationMemberRepo, [membership]);
        f.OrganizationRepo.GetByIdAsync(organization.Id, Arg.Any<CancellationToken>()).Returns(organization);

        AuthResult result = await f.Service.SwitchOrganizationAsync(
            organization.Id, null, null, CancellationToken.None);

        Assert.Equal(organization.Id, result.OrganizationId);
        Assert.Equal(organization.Name, result.OrganizationName);
        f.JwtTokenService.Received(1).GenerateAccessToken(account, organization.Id);
    }

    [Fact]
    public async Task SwitchOrganizationAsync_NullOrganizationId_IssuesTokenWithNoOrganizationWithoutMembershipCheck()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        AuthResult result = await f.Service.SwitchOrganizationAsync(null, null, null, CancellationToken.None);

        Assert.Null(result.OrganizationId);
        Assert.Null(result.OrganizationName);
        f.JwtTokenService.Received(1).GenerateAccessToken(account, null);
        await f.OrganizationMemberRepo.DidNotReceive().FirstOrDefaultAsync(
            Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>());
    }
}
