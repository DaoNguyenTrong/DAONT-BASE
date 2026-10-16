using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Application.Resources;
using StarterKit.Domain.Exceptions;

namespace StarterKit.Infrastructure.Services.Auth.External;

internal sealed class GoogleAuthProvider(
    IOptions<ExternalAuthSettings> externalAuthOptions,
    IGoogleJwtValidator googleJwtValidator) : IExternalAuthProvider
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
            payload = await googleJwtValidator.ValidateAsync(credential, settings);
        }
        catch (InvalidJwtException)
        {
            throw new UnauthorizedException(ApplicationMessages.InvalidExternalCredential);
        }

        // A token issued without the email scope has no email claim: reject it like MicrosoftAuthProvider
        // does instead of letting a null reach account matching/normalization.
        string email = payload.Email
            ?? throw new UnauthorizedException(ApplicationMessages.InvalidExternalCredential);

        return new ExternalUserInfo(payload.Subject, email, payload.Name, payload.EmailVerified);
    }
}
