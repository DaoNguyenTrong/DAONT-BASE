import { Permissions } from './permissions'

const PERMISSION_LABEL_KEYS: Record<string, string> = {
  [Permissions.OrganizationManage]: 'organizations.permissionOrganizationManage',
  [Permissions.OrganizationMembersManage]: 'organizations.permissionOrganizationMembersManage',
  [Permissions.OrganizationRolesManage]: 'organizations.permissionOrganizationRolesManage',
}

// Falls back to the raw permission code for anything not in the map above, so an unlisted
// backend permission renders instead of crashing the UI.
export function permissionLabel(code: string, t: (key: string) => string): string {
  const key = PERMISSION_LABEL_KEYS[code]
  return key ? t(key) : code
}
