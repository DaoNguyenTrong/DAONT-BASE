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

// login.microsoftonline.com/{tenant}/v2.0's discovery document reports its Issuer as the literal
// template "https://login.microsoftonline.com/{tenantid}/v2.0" when tenant is common/organizations/
// consumers — there is no single fixed issuer to compare against for those tenants, so the
// placeholder is turned into a regex instead of relying on TokenValidationParameters.ValidIssuer.
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

    // Regex.Escape("{tenantid}") only escapes the opening brace ("\{tenantid}", no backslash
    // before the closing brace), so a naive Regex.Escape(issuerTemplate).Replace("\\{tenantid\\}", ...)
    // never matches and silently leaves the literal "{tenantid}" in the pattern — which then never
    // matches any real issuer, failing every login. Splitting on the raw placeholder before escaping
    // avoids depending on Regex.Escape's exact output for the braces.
    internal static Regex BuildIssuerPattern(string issuerTemplate)
    {
        string[] segments = issuerTemplate.Split("{tenantid}");
        return new Regex("^" + string.Join("[^/]+", segments.Select(Regex.Escape)) + "$");
    }
}
