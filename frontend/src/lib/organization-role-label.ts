import type { OrganizationRole } from '@/api/types'

const ROLE_LABEL_KEYS: Record<OrganizationRole, string> = {
  Owner: 'organizations.roleOwner',
  Admin: 'organizations.roleAdmin',
  Member: 'organizations.roleMember',
}

export function organizationRoleLabelKey(role: OrganizationRole): string {
  return ROLE_LABEL_KEYS[role]
}
