using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Mappings;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Services.Auth;

public sealed class AuthService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    IJwtTokenService jwtTokenService,
    IPasswordHasher passwordHasher,
    IEmailSender emailSender,
    IEnumerable<IExternalAuthProvider> externalAuthProviders,
    IOptions<JwtSettings> jwtOptions,
    IOptions<EmailSettings> emailOptions) : IAuthService
{
    private readonly JwtSettings jwtSettings = jwtOptions.Value;
    private readonly EmailSettings emailSettings = emailOptions.Value;

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

        return await IssueTokensAsync(
            account,
            deviceInfo,
            ipAddress,
            request.KeepLoggedIn,
            DateTime.UtcNow,
            cancellationToken);
    }

    public async Task<RegisterResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        IRepository<Account, Guid> accountRepository = unitOfWork.Repository<Account, Guid>();

        await EnsureUniqueAccountAsync(accountRepository, request.Username, request.Email, cancellationToken);

        Account account = Account.Create(request.ToParams());
        account.SetPasswordHash(passwordHasher.Hash(request.Password));

        await accountRepository.AddAsync(account, cancellationToken);

        await IssueVerificationTokenAsync(account, cancellationToken);

        return new RegisterResult(account.Id, account.Email);
    }

    public async Task<AuthResult> VerifyEmailAsync(
        VerifyEmailRequest request,
        string? deviceInfo,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        string tokenHash = ComputeSha256(request.Token);

        IRepository<EmailVerificationToken, Guid> tokenRepository =
            unitOfWork.Repository<EmailVerificationToken, Guid>();

        EmailVerificationToken? token = await tokenRepository
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (token is null || !token.IsActive)
        {
            throw new UnauthorizedException(ApplicationMessages.EmailVerificationTokenInvalidOrExpired);
        }

        Account account = await unitOfWork.Repository<Account, Guid>()
            .GetByIdAsync(token.AccountId, cancellationToken)
            ?? throw new UnauthorizedException(ApplicationMessages.EmailVerificationTokenInvalidOrExpired);

        account.ConfirmEmail();
        unitOfWork.Repository<Account, Guid>().Update(account);

        token.Consume();
        tokenRepository.Update(token);

        return await IssueTokensAsync(account, deviceInfo, ipAddress, false, DateTime.UtcNow, cancellationToken);
    }

    public async Task ResendVerificationEmailAsync(
        ResendVerificationRequest request,
        CancellationToken cancellationToken)
    {
        Account? account = await unitOfWork.Repository<Account, Guid>()
            .FirstOrDefaultAsync(a => a.Email == request.Email, cancellationToken);

        if (account is null)
        {
            return;
        }

        if (account.EmailConfirmed)
        {
            throw new ConflictException(ApplicationMessages.EmailAlreadyConfirmed);
        }

        await IssueVerificationTokenAsync(account, cancellationToken);
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

        string tokenHash = ComputeSha256(refreshToken);

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

        storedToken.Revoke();
        refreshTokenRepository.Update(storedToken);

        return await IssueTokensAsync(
            account,
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

        return await IssueTokensAsync(account, deviceInfo, ipAddress, false, DateTime.UtcNow, cancellationToken);
    }

    public async Task RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        string tokenHash = ComputeSha256(refreshToken);

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

    public async Task<IReadOnlyList<SessionDto>> GetSessionsAsync(
        string? currentRefreshToken,
        CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();
        string? currentTokenHash = string.IsNullOrWhiteSpace(currentRefreshToken)
            ? null
            : ComputeSha256(currentRefreshToken);

        IReadOnlyList<RefreshToken> tokens = await unitOfWork.Repository<RefreshToken, long>()
            .ListAsync(token => token.AccountId == accountId, cancellationToken);

        return tokens
            .Where(token => token.IsActive)
            .OrderByDescending(token => token.CreatedAt)
            .Select(token => new SessionDto(
                token.Id,
                token.DeviceInfo,
                token.IpAddress,
                token.IsPersistent,
                currentTokenHash is not null && token.TokenHash == currentTokenHash,
                token.LoginAt,
                token.CreatedAt,
                token.ExpiresAt))
            .ToList();
    }

    public async Task RevokeSessionAsync(long sessionId, CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();
        IRepository<RefreshToken, long> repository = unitOfWork.Repository<RefreshToken, long>();

        RefreshToken? token = await repository.GetByIdAsync(sessionId, cancellationToken);

        if (token is null || token.AccountId != accountId)
        {
            throw new NotFoundException(nameof(RefreshToken), sessionId);
        }

        token.Revoke();
        repository.Update(token);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeOtherSessionsAsync(string? currentRefreshToken, CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();
        string? currentTokenHash = string.IsNullOrWhiteSpace(currentRefreshToken)
            ? null
            : ComputeSha256(currentRefreshToken);

        IRepository<RefreshToken, long> repository = unitOfWork.Repository<RefreshToken, long>();
        IReadOnlyList<RefreshToken> tokens = await repository
            .ListAsync(token => token.AccountId == accountId, cancellationToken);

        foreach (RefreshToken token in tokens)
        {
            if (token.IsActive && token.TokenHash != currentTokenHash)
            {
                token.Revoke();
                repository.Update(token);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private Guid GetCurrentAccountId()
    {
        if (!Guid.TryParse(currentUserService.UserId, out Guid accountId))
        {
            throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);
        }

        return accountId;
    }

    private static async Task EnsureUniqueAccountAsync(
        IRepository<Account, Guid> accountRepository,
        string username,
        string email,
        CancellationToken cancellationToken)
    {
        if (await accountRepository.FirstOrDefaultAsync(
                account => account.Username == username,
                cancellationToken) is not null)
        {
            throw new ConflictException(ApplicationMessages.AccountUsernameAlreadyExists);
        }

        if (await accountRepository.FirstOrDefaultAsync(
                account => account.Email == email,
                cancellationToken) is not null)
        {
            throw new ConflictException(ApplicationMessages.AccountEmailAlreadyExists);
        }
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

    private async Task IssueVerificationTokenAsync(Account account, CancellationToken cancellationToken)
    {
        string rawToken = GenerateRawToken();
        string tokenHash = ComputeSha256(rawToken);
        DateTime expiresAt = DateTime.UtcNow.AddHours(emailSettings.VerificationTokenExpiryHours);

        EmailVerificationToken token = EmailVerificationToken.Create(
            new EmailVerificationTokenParams(account.Id, tokenHash, expiresAt));

        await unitOfWork.Repository<EmailVerificationToken, Guid>().AddAsync(token, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        string verificationUrl = $"{emailSettings.FrontendBaseUrl.TrimEnd('/')}/verify-email?token={rawToken}";
        string body = $"""
            <p>Xin chào {account.Name},</p>
            <p>Nhấn vào liên kết bên dưới để xác thực địa chỉ email của bạn:</p>
            <p><a href="{verificationUrl}">{verificationUrl}</a></p>
            <p>Liên kết có hiệu lực trong {emailSettings.VerificationTokenExpiryHours} giờ.</p>
            """;

        await emailSender.SendAsync(account.Email, "Xác thực địa chỉ email", body, cancellationToken);
    }

    private async Task<AuthResult> IssueTokensAsync(
        Account account,
        string? deviceInfo,
        string? ipAddress,
        bool isPersistent,
        DateTime loginAt,
        CancellationToken cancellationToken)
    {
        string accessToken = jwtTokenService.GenerateAccessToken(account);
        string refreshToken = jwtTokenService.GenerateRefreshToken();
        DateTime accessTokenExpiry = DateTime.UtcNow.AddMinutes(jwtSettings.AccessTokenExpiryMinutes);
        DateTime refreshTokenExpiry = DateTime.UtcNow.AddDays(jwtSettings.RefreshTokenExpiryDays);

        RefreshToken token = RefreshToken.Create(new RefreshTokenParams(
            account.Id,
            ComputeSha256(refreshToken),
            refreshTokenExpiry,
            deviceInfo,
            ipAddress,
            isPersistent,
            loginAt));

        await unitOfWork.Repository<RefreshToken, long>().AddAsync(token, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthResult(accessToken, refreshToken, accessTokenExpiry, EntityMapper.ToDto(account), isPersistent);
    }

    private static string GenerateRawToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string ComputeSha256(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
