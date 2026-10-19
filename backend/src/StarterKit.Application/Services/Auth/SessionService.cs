using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Resources;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Services.Auth;

public sealed class SessionService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService) : ISessionService
{
    public async Task<IReadOnlyList<SessionDto>> GetSessionsAsync(
        string? currentRefreshToken,
        CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();
        Guid? currentSessionId = await ResolveCurrentSessionIdAsync(accountId, currentRefreshToken, cancellationToken);

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
                token.FamilyId == currentSessionId,
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

        // Without a session to keep, "every other session" would be every session, the caller's included.
        Guid currentSessionId = await ResolveCurrentSessionIdAsync(accountId, currentRefreshToken, cancellationToken)
            ?? throw new DomainException(DomainMessages.CurrentSessionUnidentified);

        IRepository<RefreshToken, long> repository = unitOfWork.Repository<RefreshToken, long>();
        IReadOnlyList<RefreshToken> tokens = await repository
            .ListAsync(token => token.AccountId == accountId, cancellationToken);

        foreach (RefreshToken token in tokens)
        {
            if (token.IsActive && token.FamilyId != currentSessionId)
            {
                token.Revoke();
                repository.Update(token);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // The access token names the session it was issued for. A request without one (a token minted before sessions were
    // named) is matched by its refresh cookie, which the browser sends with every request.
    private async Task<Guid?> ResolveCurrentSessionIdAsync(
        Guid accountId,
        string? currentRefreshToken,
        CancellationToken cancellationToken)
    {
        if (currentUserService.SessionId is { } sessionId)
        {
            return sessionId;
        }

        if (string.IsNullOrWhiteSpace(currentRefreshToken))
        {
            return null;
        }

        string tokenHash = TokenHash.Compute(currentRefreshToken);
        RefreshToken? cookieToken = await unitOfWork.Repository<RefreshToken, long>()
            .FirstOrDefaultAsync(token => token.AccountId == accountId && token.TokenHash == tokenHash, cancellationToken);

        return cookieToken?.FamilyId;
    }

    private Guid GetCurrentAccountId()
    {
        if (!Guid.TryParse(currentUserService.UserId, out Guid accountId))
        {
            throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);
        }

        return accountId;
    }
}
