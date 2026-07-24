using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Application.Resources;
using StarterKit.Domain.Exceptions;

namespace StarterKit.Infrastructure.Services;

public sealed class GoogleAuthProvider(IOptions<ExternalAuthSettings> externalAuthOptions) : IExternalAuthProvider
{
    private readonly ExternalAuthSettings externalAuthSettings = externalAuthOptions.Value;

    public string ProviderName => "Google";

    public async Task<ExternalUserInfo> ValidateAsync(string credential, CancellationToken cancellationToken)
    {
        GoogleJsonWebSignature.ValidationSettings settings = new()
        {
            Audience = [externalAuthSettings.Google.ClientId]
        };

        GoogleJsonWebSignature.Payload payload;

        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(credential, settings);
        }
        catch (InvalidJwtException)
        {
            throw new UnauthorizedException(ApplicationMessages.InvalidExternalCredential);
        }

        return new ExternalUserInfo(payload.Subject, payload.Email, payload.Name, payload.EmailVerified);
    }
}
