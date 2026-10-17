import type { Localized } from '../sidebar-types'

// Names of the permission codes in frontend/src/lib/permissions.ts, shown by PermissionNote on pages
// whose frontmatter sets `permission:`. tests/content-integrity.test.ts keeps the two in step.
export const permissionLabels: Record<string, Localized> = {
  'organizations.manage': { vi: 'Quản lý tổ chức', en: 'Manage organizations' },
  'organizations.members.manage': { vi: 'Quản lý thành viên', en: 'Manage members' },
  'organizations.roles.manage': { vi: 'Quản lý vai trò', en: 'Manage roles' },
}
