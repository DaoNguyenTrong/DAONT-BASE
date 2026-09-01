using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Mappings;
using StarterKit.Application.Common.Settings;
using StarterKit.Application.Resources;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Services.Auth;

public sealed class RegistrationService(
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IEmailSender emailSender,
    IOptions<EmailSettings> emailOptions,
    ITokenIssuer tokenIssuer,
    ILogger<RegistrationService> logger) : IRegistrationService
{
    private readonly EmailSettings emailSettings = emailOptions.Value;

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
        string tokenHash = TokenHash.Compute(request.Token);

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

        Guid? organizationId = await tokenIssuer.ResolveDefaultOrganizationIdAsync(account.Id, cancellationToken);

        return await tokenIssuer.IssueTokensAsync(account, organizationId, deviceInfo, ipAddress, false, DateTime.UtcNow, familyId: null, cancellationToken);
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

    private async Task IssueVerificationTokenAsync(Account account, CancellationToken cancellationToken)
    {
        string rawToken = GenerateRawToken();
        string tokenHash = TokenHash.Compute(rawToken);
        DateTime expiresAt = DateTime.UtcNow.AddHours(emailSettings.VerificationTokenExpiryHours);

        EmailVerificationToken token = EmailVerificationToken.Create(
            new EmailVerificationTokenParams(account.Id, tokenHash, expiresAt));

        await unitOfWork.Repository<EmailVerificationToken, Guid>().AddAsync(token, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Frontend router uses createWebHashHistory() (hash-based routing) — a plain path URL
        // resolves to "/" before vue-router ever sees "verify-email", tripping the home route's
        // requiresAuth guard and bouncing the (not-yet-authenticated) user to /login instead of
        // VerifyEmailView. The route must live after "#/" so vue-router picks it up.
        string verificationUrl = $"{emailSettings.FrontendBaseUrl.TrimEnd('/')}/#/verify-email?token={rawToken}";
        string body = $"""
            <p>Xin chào {account.Name},</p>
            <p>Nhấn vào liên kết bên dưới để xác thực địa chỉ email của bạn:</p>
            <p><a href="{verificationUrl}">{verificationUrl}</a></p>
            <p>Liên kết có hiệu lực trong {emailSettings.VerificationTokenExpiryHours} giờ.</p>
            """;

        try
        {
            await emailSender.SendAsync(account.Email, "Xác thực địa chỉ email", body, cancellationToken);
        }
        catch (Exception exception)
        {
            // Account + token đã commit — lỗi gửi mail (SMTP chưa cấu hình/đang lỗi) không được
            // biến 1 RegisterAsync/ResendVerificationEmailAsync thành công thành lỗi phía caller,
            // nếu không user sẽ bị kẹt: retry đăng ký cùng username thì gặp 409 "đã tồn tại" mà
            // không có lối thoát. User dùng "Gửi lại email xác thực" để thử lại sau khi SMTP ổn.
            logger.LogError(exception, "Failed to send verification email to account {AccountId}", account.Id);
        }
    }

    private static string GenerateRawToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
