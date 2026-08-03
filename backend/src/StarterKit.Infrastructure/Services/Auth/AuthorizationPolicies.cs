namespace StarterKit.Infrastructure.Services.Auth;

/// <summary>
/// Well-known policy names for checks that aren't keyed by a <see cref="Common.Authorization.Permissions"/>
/// code (e.g. "any active member, no specific permission required") — referenced from
/// <c>StarterKit.API</c> controllers via <c>[Authorize(Policy = AuthorizationPolicies.X)]</c>.
/// </summary>
public static class AuthorizationPolicies
{
    public const string OrganizationMember = "OrganizationMember";
}
