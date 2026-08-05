using Microsoft.AspNetCore.Authorization;

namespace StarterKit.Infrastructure.Services.Auth;

internal sealed class ActiveOrganizationPermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode;
}
