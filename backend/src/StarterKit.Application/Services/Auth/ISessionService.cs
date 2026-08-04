namespace StarterKit.Application.Services.Auth;

public interface ISessionService
{
    Task<IReadOnlyList<SessionDto>> GetSessionsAsync(
        string? currentRefreshToken,
        CancellationToken cancellationToken);

    Task RevokeSessionAsync(long sessionId, CancellationToken cancellationToken);

    Task RevokeOtherSessionsAsync(string? currentRefreshToken, CancellationToken cancellationToken);
}
