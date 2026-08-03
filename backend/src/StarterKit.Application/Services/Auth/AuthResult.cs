using StarterKit.Application.Services.Accounts;

namespace StarterKit.Application.Services.Auth;

public sealed record AuthResult(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry,
    AccountDto Account,
    bool IsPersistent,
    Guid? OrganizationId,
    string? OrganizationName,
    IReadOnlyList<string> Permissions);
