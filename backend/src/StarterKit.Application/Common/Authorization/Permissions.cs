namespace StarterKit.Application.Common.Authorization;

public static class Permissions
{
    public const string OrganizationManage = "organizations.manage";
    public const string OrganizationMembersManage = "organizations.members.manage";
    public const string OrganizationRolesManage = "organizations.roles.manage";

    public static readonly IReadOnlyList<string> All =
    [
        OrganizationManage,
        OrganizationMembersManage,
        OrganizationRolesManage
    ];
}
