using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Infrastructure.Services;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IUnitOfWork unitOfWork)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "ApiKey";
    private const string ApiKeyHeader = "X-Api-Key";
    private const int PrefixLength = 8;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeader, out Microsoft.Extensions.Primitives.StringValues rawKeyValues))
        {
            return AuthenticateResult.NoResult();
        }

        string rawKey = rawKeyValues.ToString();

        if (rawKey.Length < PrefixLength)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        string prefix = rawKey[..PrefixLength];
        string keyHash = ComputeSha256(rawKey);

        ApiKey? apiKey = await unitOfWork.Repository<ApiKey, Guid>()
            .FirstOrDefaultAsync(k => k.KeyPrefix == prefix && k.IsActive, Context.RequestAborted);

        if (apiKey is null || apiKey.KeyHash != keyHash)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        Claim[] claims =
        [
            new Claim(ClaimTypes.Name, $"apikey:{apiKey.Name}"),
            new Claim(ApiKeyClaims.KeyId, apiKey.Id.ToString())
        ];

        ClaimsIdentity identity = new(claims, SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }

    private static string ComputeSha256(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
