using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Application.Resources;
using StarterKit.Application.Services.Accounts;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Services.Auth;

public sealed class AuthService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    ITokenIssuer tokenIssuer,
    ITenantAccessService tenantAccessService,
    IPasswordHasher passwordHasher,
    IEnumerable<IExternalAuthProvider> externalAuthProviders,
    IPasswordAttemptStore passwordAttemptStore,
    IOptions<LoginLockoutSettings> lockoutOptions,
    IOptions<JwtSettings> jwtOptions) : IAuthService
{
    private readonly JwtSettings jwtSettings = jwtOptions.Value;
    private readonly LoginLockoutSettings lockoutSettings = lockoutOptions.Value;

    public async Task<AuthResult> LoginAsync(
        LoginRequest request,
        string? deviceInfo,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        string username = Account.NormalizeUsername(request.Username);
        Account account = await unitOfWork.Repository<Account, Guid>()
            .FirstOrDefaultAsync(account => account.Username == username, cancellationToken)
            ?? throw new UnauthorizedException(ApplicationMessages.InvalidUsernameOrPassword);

        // A deactivated or password-less account has no password to guess: the generic message, nothing
        // counted — a lock message would say more about it than "invalid" does.
        if (!account.Status || string.IsNullOrWhiteSpace(account.PasswordHash))
        {
            throw new UnauthorizedException(ApplicationMessages.InvalidUsernameOrPassword);
        }

        DateTime utcNow = DateTime.UtcNow;

        // Reserved before the password is checked, so parallel guesses can't all slip in under the limit.
        // The lock is reported before the check too: refusing only a correct password would tell a guesser
        // which guess was right. It reveals no more than /register's "username taken" already does.
        int? attempt = await passwordAttemptStore.ReserveAttemptAsync(
            PasswordAttemptPurpose.Login,
            account.Id,
            utcNow,
            TimeSpan.FromMinutes(lockoutSettings.WindowMinutes),
            cancellationToken);
        if (attempt is not { } attemptNumber || attemptNumber > lockoutSettings.MaxFailedAttempts)
        {
            throw new UnauthorizedException(ApplicationMessages.AccountTemporarilyLocked);
        }

        if (!passwordHasher.Verify(request.Password, account.PasswordHash))
        {
            if (attemptNumber == lockoutSettings.MaxFailedAttempts)
            {
                // Not the request's token: a guesser who drops the connection during the hash check would
                // otherwise cancel the lock write and keep guessing once the window rolls over.
                await passwordAttemptStore.LockAsync(
                    PasswordAttemptPurpose.Login,
                    account.Id,
                    utcNow.AddMinutes(lockoutSettings.LockDurationMinutes),
                    CancellationToken.None);
            }

            throw new UnauthorizedException(ApplicationMessages.InvalidUsernameOrPassword);
        }

        // The password is right even if the account can't sign in yet (unconfirmed email) — counting on
        // would lock it out on its own.
        await passwordAttemptStore.ClearAsync(PasswordAttemptPurpose.Login, account.Id, cancellationToken);

        if (!account.EmailConfirmed)
        {
            throw new UnauthorizedException(ApplicationMessages.EmailNotConfirmed);
        }

        Guid? organizationId = await tokenIssuer.ResolveDefaultOrganizationIdAsync(account.Id, cancellationToken);

        return await tokenIssuer.IssueTokensAsync(
            account,
            organizationId,
            deviceInfo,
            ipAddress,
            request.KeepLoggedIn,
            DateTime.UtcNow,
            familyId: null,
            cancellationToken);
    }

    public async Task<AuthResult> RefreshTokenAsync(
        string refreshToken,
        string? deviceInfo,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedException(ApplicationMessages.RefreshTokenRequired);
        }

        string tokenHash = TokenHash.Compute(refreshToken);

        IRepository<RefreshToken, long> refreshTokenRepository = unitOfWork.Repository<RefreshToken, long>();
        RefreshToken storedToken = await refreshTokenRepository
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken)
            ?? throw new UnauthorizedException(ApplicationMessages.InvalidRefreshToken);

        RefreshToken rotatingToken = storedToken.IsActive
            ? storedToken
            : await ResolveInactiveRefreshTokenAsync(refreshTokenRepository, storedToken, cancellationToken);

        Account account = await unitOfWork.Repository<Account, Guid>()
            .GetByIdAsync(rotatingToken.AccountId, cancellationToken)
            ?? throw new UnauthorizedException(ApplicationMessages.InvalidRefreshToken);

        if (!account.Status)
        {
            throw new UnauthorizedException(ApplicationMessages.InvalidRefreshToken);
        }

        if (rotatingToken.OrganizationId is { } organizationId &&
            !await tenantAccessService.HasActiveAccessAsync(account.Id, organizationId, cancellationToken))
        {
            throw new UnauthorizedException(ApplicationMessages.InvalidRefreshToken);
        }

        rotatingToken.Revoke();
        refreshTokenRepository.Update(rotatingToken);

        return await tokenIssuer.IssueTokensAsync(
            account,
            rotatingToken.OrganizationId,
            deviceInfo,
            ipAddress,
            rotatingToken.IsPersistent,
            rotatingToken.LoginAt,
            rotatingToken.FamilyId,
            cancellationToken);
    }

    /// <summary>
    /// An inactive refresh token isn't necessarily a forgery: tabs share the cookie, so another tab's refresh may have
    /// just rotated it. If its family still has an active token and this one was revoked within the grace window
    /// (<see cref="JwtSettings.RefreshTokenReuseGraceSeconds"/>), rotate from that token; else revoke the whole family.
    /// </summary>
    private async Task<RefreshToken> ResolveInactiveRefreshTokenAsync(
        IRepository<RefreshToken, long> repository,
        RefreshToken presentedToken,
        CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        Guid accountId = presentedToken.AccountId;
        Guid familyId = presentedToken.FamilyId;

        RefreshToken? activeInFamily = await repository.FirstOrDefaultAsync(
            token => token.AccountId == accountId
                && token.FamilyId == familyId
                && token.RevokedAt == null
                && token.ExpiresAt > now,
            cancellationToken);

        bool withinGrace = presentedToken.RevokedAt is { } revokedAt
            && revokedAt >= now.AddSeconds(-jwtSettings.RefreshTokenReuseGraceSeconds);

        if (activeInFamily is not null && withinGrace)
        {
            return activeInFamily;
        }

        if (activeInFamily is not null)
        {
            // Stale token replayed while the family is still live — assume it leaked.
            // Burn every active token in the family so both the attacker and the
            // legitimate user are forced to re-authenticate.
            IReadOnlyList<RefreshToken> activeFamilyTokens = await repository.ListAsync(
                token => token.AccountId == accountId
                    && token.FamilyId == familyId
                    && token.RevokedAt == null,
                cancellationToken);

            foreach (RefreshToken familyToken in activeFamilyTokens)
            {
                familyToken.Revoke();
                repository.Update(familyToken);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        throw new UnauthorizedException(ApplicationMessages.InvalidRefreshToken);
    }

    public async Task<AuthResult> ExternalLoginAsync(
        string provider,
        ExternalLoginRequest request,
        string? deviceInfo,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        IExternalAuthProvider authProvider = externalAuthProviders.FirstOrDefault(
            p => string.Equals(p.ProviderName, provider, StringComparison.OrdinalIgnoreCase))
            ?? throw new DomainException(ApplicationMessages.ExternalLoginProviderNotSupported);

        ExternalUserInfo userInfo = await authProvider.ValidateAsync(request.Credential, cancellationToken);

        if (!userInfo.EmailVerified)
        {
            throw new UnauthorizedException(ApplicationMessages.ExternalLoginEmailNotVerifiedByProvider);
        }

        IRepository<ExternalLogin, Guid> externalLoginRepository = unitOfWork.Repository<ExternalLogin, Guid>();
        IRepository<Account, Guid> accountRepository = unitOfWork.Repository<Account, Guid>();

        ExternalLogin? existingLogin = await externalLoginRepository.FirstOrDefaultAsync(
            l => l.Provider == authProvider.ProviderName && l.ProviderUserId == userInfo.ProviderUserId,
            cancellationToken);

        Account account;

        if (existingLogin is not null)
        {
            account = await accountRepository.GetByIdAsync(existingLogin.AccountId, cancellationToken)
                ?? throw new UnauthorizedException(ApplicationMessages.InvalidUsernameOrPassword);
        }
        else
        {
            string email = Account.NormalizeEmail(userInfo.Email);
            Account? matchedAccount = await accountRepository.FirstOrDefaultAsync(
                a => a.Email == email, cancellationToken);

            if (matchedAccount is not null)
            {
                if (!matchedAccount.EmailConfirmed)
                {
                    throw new ConflictException(ApplicationMessages.ExternalLoginEmailNotConfirmed);
                }

                account = matchedAccount;
            }
            else
            {
                string username = await GenerateUniqueUsernameAsync(
                    accountRepository, userInfo.Email, cancellationToken);

                account = Account.Create(new AccountParams(
                    Name: string.IsNullOrWhiteSpace(userInfo.Name) ? username : userInfo.Name,
                    Username: username,
                    Email: email));
                account.ConfirmEmail();

                await accountRepository.AddAsync(account, cancellationToken);
            }

            ExternalLogin login = ExternalLogin.Create(new ExternalLoginParams(
                account.Id, authProvider.ProviderName, userInfo.ProviderUserId, userInfo.Email));
            await externalLoginRepository.AddAsync(login, cancellationToken);
        }

        // The lock only defends against password guessing; the provider has just proven identity.
        await passwordAttemptStore.ClearAsync(PasswordAttemptPurpose.Login, account.Id, cancellationToken);

        Guid? organizationId = await tokenIssuer.ResolveDefaultOrganizationIdAsync(account.Id, cancellationToken);

        return await tokenIssuer.IssueTokensAsync(account, organizationId, deviceInfo, ipAddress, false, DateTime.UtcNow, familyId: null, cancellationToken);
    }

    public async Task RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        string tokenHash = TokenHash.Compute(refreshToken);

        IRepository<RefreshToken, long> repository = unitOfWork.Repository<RefreshToken, long>();
        RefreshToken? storedToken = await repository
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null)
        {
            return;
        }

        storedToken.Revoke();
        repository.Update(storedToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthResult> SwitchOrganizationAsync(
        Guid? organizationId,
        string? deviceInfo,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();

        if (organizationId is { } targetOrganizationId)
        {
            OrganizationMember? member = await unitOfWork.Repository<OrganizationMember, Guid>()
                .FirstOrDefaultAsync(
                    m => m.OrganizationId == targetOrganizationId && m.AccountId == accountId && m.IsActive,
                    cancellationToken);

            Organization? organization = member is null
                ? null
                : await unitOfWork.Repository<Organization, Guid>().GetByIdAsync(targetOrganizationId, cancellationToken);

            if (member is null || organization is null || !organization.Status)
            {
                throw new ForbiddenException(ApplicationMessages.OrganizationAccessDenied);
            }
        }

        Account account = await unitOfWork.Repository<Account, Guid>().GetByIdAsync(accountId, cancellationToken)
            ?? throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);

        // Same sign-in, different organization: the session keeps its family and its previous token is retired, so
        // the device is listed once. A token minted before sessions were named (no session claim) starts a new one.
        Guid? sessionId = currentUserService.SessionId;
        if (sessionId is { } currentSessionId)
        {
            IRepository<RefreshToken, long> refreshTokenRepository = unitOfWork.Repository<RefreshToken, long>();
            IReadOnlyList<RefreshToken> previousTokens = await refreshTokenRepository.ListAsync(
                token => token.AccountId == accountId && token.FamilyId == currentSessionId && token.RevokedAt == null,
                cancellationToken);

            foreach (RefreshToken previousToken in previousTokens)
            {
                previousToken.Revoke();
                refreshTokenRepository.Update(previousToken);
            }
        }

        return await tokenIssuer.IssueTokensAsync(account, organizationId, deviceInfo, ipAddress, true, DateTime.UtcNow, familyId: sessionId, cancellationToken);
    }

    private Guid GetCurrentAccountId()
    {
        if (!Guid.TryParse(currentUserService.UserId, out Guid accountId))
        {
            throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);
        }

        return accountId;
    }

    private static async Task<string> GenerateUniqueUsernameAsync(
        IRepository<Account, Guid> accountRepository,
        string email,
        CancellationToken cancellationToken)
    {
        string baseUsername = Account.NormalizeUsername(email.Split('@')[0]);
        string candidate = baseUsername;
        int suffix = 0;

        while (await accountRepository.FirstOrDefaultAsync(
            a => a.Username == candidate, cancellationToken) is not null)
        {
            suffix++;
            candidate = $"{baseUsername}{suffix}";
        }

        return candidate;
    }
}
