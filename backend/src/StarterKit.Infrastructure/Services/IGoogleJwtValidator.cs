using Google.Apis.Auth;

namespace StarterKit.Infrastructure.Services;

internal interface IGoogleJwtValidator
{
    Task<GoogleJsonWebSignature.Payload> ValidateAsync(
        string credential, GoogleJsonWebSignature.ValidationSettings settings);
}

internal sealed class GoogleJwtValidator : IGoogleJwtValidator
{
    public Task<GoogleJsonWebSignature.Payload> ValidateAsync(
        string credential, GoogleJsonWebSignature.ValidationSettings settings)
        => GoogleJsonWebSignature.ValidateAsync(credential, settings);
}
