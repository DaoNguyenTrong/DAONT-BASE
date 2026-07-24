using FeedbackHub.Application.Services.Accounts;

namespace FeedbackHub.Application.Services.Auth;

public sealed record AuthResult(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry,
    AccountDto Account,
    bool IsPersistent);
