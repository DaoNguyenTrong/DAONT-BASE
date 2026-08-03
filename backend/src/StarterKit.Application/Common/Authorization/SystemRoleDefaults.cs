using StarterKit.Domain.Entities;

namespace StarterKit.Application.Common.Authorization;

/// <summary>
/// Seeded permission sets for system roles, keyed by <see cref="SystemRoleKind"/>.
/// Owner is intentionally absent — its permission set is resolved dynamically as
/// <see cref="Permissions.All"/> so it can never fall behind a permission added later.
/// </summary>
public static class SystemRoleDefaults
{
    public static readonly IReadOnlyDictionary<SystemRoleKind, IReadOnlyList<string>> PermissionsByKind =
        new Dictionary<SystemRoleKind, IReadOnlyList<string>>
        {
            [SystemRoleKind.Admin] = [Permissions.OrganizationMembersManage],
            [SystemRoleKind.Member] = []
        };
}
