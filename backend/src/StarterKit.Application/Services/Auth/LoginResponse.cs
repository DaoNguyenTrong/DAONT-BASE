using StarterKit.Application.Services.Accounts;

namespace StarterKit.Application.Services.Auth;

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry,
    AccountDto Account,
    Guid? OrganizationId,
    string? OrganizationName);
