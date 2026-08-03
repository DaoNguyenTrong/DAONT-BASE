using Microsoft.AspNetCore.Authorization;

namespace StarterKit.Infrastructure.Services.Auth;

internal sealed class OrganizationPermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode;
}
