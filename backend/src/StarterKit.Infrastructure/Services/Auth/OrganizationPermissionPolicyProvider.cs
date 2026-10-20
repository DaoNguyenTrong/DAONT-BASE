using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Authorization;

namespace StarterKit.Infrastructure.Services.Auth;

/// <summary>
/// Turns any <see cref="Permissions"/> code used as a policy name into a policy (no per-permission <c>AddPolicy</c>):
/// <see cref="Permissions.ActiveOrganizationScoped"/> codes get <see cref="ActiveOrganizationPermissionRequirement"/>,
/// the rest <see cref="OrganizationPermissionRequirement"/> (<c>{id}</c> route); other names use the default provider.
/// </summary>
internal sealed class OrganizationPermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (Permissions.ActiveOrganizationScoped.Contains(policyName))
        {
            AuthorizationPolicy activeOrgPolicy = new AuthorizationPolicyBuilder()
                .AddRequirements(new ActiveOrganizationPermissionRequirement(policyName))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(activeOrgPolicy);
        }

        if (Permissions.All.Contains(policyName))
        {
            AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new OrganizationPermissionRequirement(policyName))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return fallback.GetPolicyAsync(policyName);
    }
}
