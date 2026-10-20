namespace StarterKit.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }

    string? UserName { get; }

    bool IsApiKeyCaller { get; }

    Guid? ApiKeyId { get; }

    /// <summary>The login session the access token was issued for; null for API-key callers and tokens minted before sessions were named.</summary>
    Guid? SessionId { get; }
}
