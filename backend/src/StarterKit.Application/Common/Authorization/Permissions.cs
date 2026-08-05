namespace StarterKit.Application.Common.Authorization;

public static class Permissions
{
    public const string OrganizationManage = "organizations.manage";
    public const string OrganizationMembersManage = "organizations.members.manage";
    public const string OrganizationRolesManage = "organizations.roles.manage";
    public const string FilesManage = "files.manage";
    public const string ApiKeysManage = "apikeys.manage";
    public const string AuditLogsView = "auditlogs.view";
    public const string SettingsManage = "settings.manage";

    /// <summary>
    /// Permission codes checked against the caller's active organization (the JWT <c>org_id</c>
    /// claim, resolved via <c>ICurrentTenantProvider</c>) rather than an <c>{id}</c> route segment —
    /// see <c>ActiveOrganizationPermissionAuthorizationHandler</c>. Every code here must also
    /// appear in <see cref="All"/>.
    /// </summary>
    public static readonly IReadOnlyList<string> ActiveOrganizationScoped =
    [
        FilesManage,
        ApiKeysManage,
        AuditLogsView,
        SettingsManage
    ];

    public static readonly IReadOnlyList<string> All =
    [
        OrganizationManage,
        OrganizationMembersManage,
        OrganizationRolesManage,
        FilesManage,
        ApiKeysManage,
        AuditLogsView,
        SettingsManage
    ];
}
