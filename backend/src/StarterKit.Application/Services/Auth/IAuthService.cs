namespace StarterKit.Application.Services.Auth;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(
        LoginRequest request,
        string? deviceInfo,
        string? ipAddress,
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

    Task<AuthResult> SwitchOrganizationAsync(
        Guid? organizationId,
        string? deviceInfo,
        string? ipAddress,
        CancellationToken cancellationToken);
}
