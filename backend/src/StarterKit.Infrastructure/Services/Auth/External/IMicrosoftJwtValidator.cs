using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace StarterKit.Infrastructure.Services.Auth.External;

internal sealed record MicrosoftTokenPayload(string Subject, string? Email, string? Name);

internal interface IMicrosoftJwtValidator
{
    Task<MicrosoftTokenPayload> ValidateAsync(
        string credential, string tenantId, string clientId, CancellationToken cancellationToken);
}

// For common/organizations/consumers tenants the discovery document's Issuer is the literal template
// "https://login.microsoftonline.com/{tenantid}/v2.0" — no fixed issuer exists, so the placeholder becomes
// a regex instead of TokenValidationParameters.ValidIssuer.
internal sealed class MicrosoftJwtValidator : IMicrosoftJwtValidator
{
    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> ConfigManagers = new();

    public async Task<MicrosoftTokenPayload> ValidateAsync(
        string credential, string tenantId, string clientId, CancellationToken cancellationToken)
    {
        ConfigurationManager<OpenIdConnectConfiguration> configManager = ConfigManagers.GetOrAdd(tenantId, id =>
            new ConfigurationManager<OpenIdConnectConfiguration>(
                $"https://login.microsoftonline.com/{id}/v2.0/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever()));

        OpenIdConnectConfiguration config = await configManager.GetConfigurationAsync(cancellationToken);

        Regex issuerPattern = BuildIssuerPattern(config.Issuer);

        TokenValidationParameters validationParameters = new()
        {
            ValidateIssuer = true,
            IssuerValidator = (issuer, _, _) => issuerPattern.IsMatch(issuer)
                ? issuer
                : throw new SecurityTokenInvalidIssuerException($"Issuer '{issuer}' is not valid."),
            ValidateAudience = true,
            ValidAudience = clientId,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        JwtSecurityTokenHandler handler = new();
        ClaimsPrincipal principal = handler.ValidateToken(credential, validationParameters, out _);

        string subject = principal.FindFirst("oid")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new SecurityTokenException("Token is missing a subject claim.");
        string? email = principal.FindFirst("email")?.Value
            ?? principal.FindFirst("preferred_username")?.Value;
        string? name = principal.FindFirst("name")?.Value;

        return new MicrosoftTokenPayload(subject, email, name);
    }

    // Regex.Escape escapes only the opening brace ("\{tenantid}"), so escaping first and then replacing the
    // placeholder silently never matches — failing every login. Split on the raw placeholder before escaping.
    internal static Regex BuildIssuerPattern(string issuerTemplate)
    {
        string[] segments = issuerTemplate.Split("{tenantid}");
        return new Regex("^" + string.Join("[^/]+", segments.Select(Regex.Escape)) + "$");
    }
}
