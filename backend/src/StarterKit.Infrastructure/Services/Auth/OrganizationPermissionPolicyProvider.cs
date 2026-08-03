using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Authorization;

namespace StarterKit.Infrastructure.Services.Auth;

/// <summary>
/// Resolves any policy name that matches a <see cref="Permissions"/> code into an ad-hoc
/// <see cref="OrganizationPermissionRequirement"/>-backed policy, so controllers can write
/// <c>[Authorize(Policy = Permissions.X)]</c> without a matching <c>AddPolicy</c> call per
/// permission. Falls through to the default provider for everything else (the statically
/// registered <see cref="AuthorizationPolicies.OrganizationMember"/> policy and the bare
/// default <c>[Authorize]</c> policy).
/// </summary>
internal sealed class OrganizationPermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
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
