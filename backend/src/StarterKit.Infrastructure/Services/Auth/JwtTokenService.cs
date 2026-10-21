using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Domain.Entities;

namespace StarterKit.Infrastructure.Services.Auth;

public sealed class JwtTokenService(IOptions<JwtSettings> options) : IJwtTokenService
{
    private readonly JwtSettings settings = options.Value;

    public string GenerateAccessToken(Account account, Guid? organizationId, Guid sessionId)
    {
        if (settings.Audiences.Length == 0)
        {
            throw new InvalidOperationException("At least one JWT audience is required.");
        }

        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(settings.SecretKey));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);
        List<Claim> claims = new()
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.Username),
            new(ClaimTypes.Email, account.Email),
            new(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (organizationId is { } orgId)
        {
            claims.Add(new Claim(IJwtTokenService.OrganizationIdClaimType, orgId.ToString()));
        }

        claims.Add(new Claim(IJwtTokenService.SessionIdClaimType, sessionId.ToString()));

        JwtSecurityToken token = new(
            issuer: settings.Issuer,
            audience: settings.Audiences[0],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(settings.AccessTokenExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(64);

        return Convert.ToBase64String(bytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }
}
