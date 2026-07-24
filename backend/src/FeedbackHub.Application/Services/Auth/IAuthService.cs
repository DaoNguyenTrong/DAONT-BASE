namespace FeedbackHub.Application.Services.Auth;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(
        LoginRequest request,
        string? deviceInfo,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<RegisterResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken);

    Task<AuthResult> VerifyEmailAsync(
        VerifyEmailRequest request,
        string? deviceInfo,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task ResendVerificationEmailAsync(
        ResendVerificationRequest request,
        CancellationToken cancellationToken);

    Task<AuthResult> RefreshTokenAsync(
        string refreshToken,
        string? deviceInfo,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken);

    Task<AuthResult> ExternalLoginAsync(
        string provider,
        ExternalLoginRequest request,
        string? deviceInfo,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionDto>> GetSessionsAsync(
        string? currentRefreshToken,
        CancellationToken cancellationToken);

    Task RevokeSessionAsync(long sessionId, CancellationToken cancellationToken);

    Task RevokeOtherSessionsAsync(string? currentRefreshToken, CancellationToken cancellationToken);
}
