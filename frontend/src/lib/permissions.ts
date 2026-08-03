// Hand-kept in sync with backend/src/StarterKit.Application/Common/Authorization/Permissions.cs
// — plain string constants have no codegen path from the OpenAPI spec.
export const Permissions = {
  OrganizationManage: 'organizations.manage',
  OrganizationMembersManage: 'organizations.members.manage',
  OrganizationRolesManage: 'organizations.roles.manage',
} as const
