namespace StarterKit.Application.Common.Interfaces;

public sealed record ExternalUserInfo(string ProviderUserId, string Email, string? Name, bool EmailVerified);

public interface IExternalAuthProvider
{
    string ProviderName { get; }

    Task<ExternalUserInfo> ValidateAsync(string credential, CancellationToken cancellationToken);
}
