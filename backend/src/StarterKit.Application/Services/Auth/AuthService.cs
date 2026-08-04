using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Resources;
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
    IEnumerable<IExternalAuthProvider> externalAuthProviders) : IAuthService
{
    public async Task<AuthResult> LoginAsync(
        LoginRequest request,
        string? deviceInfo,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        Account account = await unitOfWork.Repository<Account, Guid>()
            .FirstOrDefaultAsync(account => account.Username == request.Username, cancellationToken)
            ?? throw new UnauthorizedException(ApplicationMessages.InvalidUsernameOrPassword);

        if (!account.Status ||
            string.IsNullOrWhiteSpace(account.PasswordHash) ||
            !passwordHasher.Verify(request.Password, account.PasswordHash))
        {
            throw new UnauthorizedException(ApplicationMessages.InvalidUsernameOrPassword);
        }

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

        if (!storedToken.IsActive)
        {
            throw new UnauthorizedException(ApplicationMessages.InvalidRefreshToken);
        }

        Account account = await unitOfWork.Repository<Account, Guid>()
            .GetByIdAsync(storedToken.AccountId, cancellationToken)
            ?? throw new UnauthorizedException(ApplicationMessages.InvalidRefreshToken);

        if (!account.Status)
        {
            throw new UnauthorizedException(ApplicationMessages.InvalidRefreshToken);
        }

        if (storedToken.OrganizationId is { } organizationId &&
            !await tenantAccessService.HasActiveAccessAsync(account.Id, organizationId, cancellationToken))
        {
            throw new UnauthorizedException(ApplicationMessages.InvalidRefreshToken);
        }

        storedToken.Revoke();
        refreshTokenRepository.Update(storedToken);

        return await tokenIssuer.IssueTokensAsync(
            account,
            storedToken.OrganizationId,
            deviceInfo,
            ipAddress,
            storedToken.IsPersistent,
            storedToken.LoginAt,
            cancellationToken);
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
            Account? matchedAccount = await accountRepository.FirstOrDefaultAsync(
                a => a.Email == userInfo.Email, cancellationToken);

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
                    Email: userInfo.Email,
                    Status: true));
                account.ConfirmEmail();

                await accountRepository.AddAsync(account, cancellationToken);
            }

            ExternalLogin login = ExternalLogin.Create(new ExternalLoginParams(
                account.Id, authProvider.ProviderName, userInfo.ProviderUserId, userInfo.Email));
            await externalLoginRepository.AddAsync(login, cancellationToken);
        }

        Guid? organizationId = await tokenIssuer.ResolveDefaultOrganizationIdAsync(account.Id, cancellationToken);

        return await tokenIssuer.IssueTokensAsync(account, organizationId, deviceInfo, ipAddress, false, DateTime.UtcNow, cancellationToken);
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

        return await tokenIssuer.IssueTokensAsync(account, organizationId, deviceInfo, ipAddress, true, DateTime.UtcNow, cancellationToken);
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
        string baseUsername = email.Split('@')[0];
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
