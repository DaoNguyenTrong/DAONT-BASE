import { appPermissionLabels } from '../permission-labels.app'
import type { Localized } from '../sidebar-types'

// [base] Names of the base's permission codes (frontend/src/lib/permissions.ts); a fork names its
// own in .vitepress/permission-labels.app.ts. tests/content-integrity.test.ts checks that every
// code a page requires is known and labelled.
const basePermissionLabels: Record<string, Localized> = {
  'organizations.manage': { vi: 'Quản lý tổ chức', en: 'Manage organization' },
  'organizations.members.manage': { vi: 'Quản lý thành viên', en: 'Manage members' },
  'organizations.roles.manage': { vi: 'Quản lý vai trò', en: 'Manage roles' },
  'files.manage': { vi: 'Quản lý tệp', en: 'Manage files' },
  'apikeys.manage': { vi: 'Quản lý khoá API', en: 'Manage API keys' },
  'auditlogs.view': { vi: 'Xem nhật ký hoạt động', en: 'View audit logs' },
  'settings.manage': { vi: 'Quản lý cài đặt', en: 'Manage settings' },
}

export const permissionLabels: Record<string, Localized> = {
  ...basePermissionLabels,
  ...appPermissionLabels,
}
