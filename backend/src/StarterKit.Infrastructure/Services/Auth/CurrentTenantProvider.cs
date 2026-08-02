using Microsoft.AspNetCore.Http;
using StarterKit.Application.Common.Interfaces;

namespace StarterKit.Infrastructure.Services.Auth;

public sealed class CurrentTenantProvider(IHttpContextAccessor httpContextAccessor) : ICurrentTenantProvider
{
    public Guid? OrganizationId
    {
        get
        {
            string? value = httpContextAccessor.HttpContext?.User
                .FindFirst(IJwtTokenService.OrganizationIdClaimType)?.Value;

            return Guid.TryParse(value, out Guid organizationId) ? organizationId : null;
        }
    }
}
