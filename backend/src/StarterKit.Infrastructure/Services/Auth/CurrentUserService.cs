using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using StarterKit.Application.Common.Interfaces;

namespace StarterKit.Infrastructure.Services.Auth;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public string? UserId => httpContextAccessor.HttpContext?.User
        .FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public string? UserName => httpContextAccessor.HttpContext?.User
        .FindFirst(ClaimTypes.Name)?.Value;

    public bool IsApiKeyCaller =>
        httpContextAccessor.HttpContext?.User.HasClaim(c => c.Type == ApiKeyClaims.KeyId) ?? false;

    public Guid? ApiKeyId =>
        Guid.TryParse(
            httpContextAccessor.HttpContext?.User.FindFirst(ApiKeyClaims.KeyId)?.Value,
            out Guid id)
            ? id
            : null;
}
