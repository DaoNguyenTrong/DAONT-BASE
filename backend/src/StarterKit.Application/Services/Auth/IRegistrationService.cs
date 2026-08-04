namespace StarterKit.Application.Services.Auth;

public interface IRegistrationService
{
    Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<AuthResult> VerifyEmailAsync(
        VerifyEmailRequest request,
        string? deviceInfo,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task ResendVerificationEmailAsync(ResendVerificationRequest request, CancellationToken cancellationToken);
}
