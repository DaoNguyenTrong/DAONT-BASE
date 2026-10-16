import type { OrganizationDto, RoleDto } from '@/api/types'

// An organization the signed-in account owns, with every management permission.
export function makeOrganization(overrides: Partial<OrganizationDto> = {}): OrganizationDto {
  return {
    id: 'org-1',
    name: 'Acme Inc',
    slug: 'acme',
    status: true,
    myRoleNames: ['Owner'],
    myPermissionCodes: [
      'organizations.manage',
      'organizations.members.manage',
      'organizations.roles.manage',
    ],
    ...overrides,
  }
}

export function makeRole(overrides: Partial<RoleDto> = {}): RoleDto {
  return { id: 'role-owner', name: 'Owner', isSystem: true, permissionCodes: [], ...overrides }
}
