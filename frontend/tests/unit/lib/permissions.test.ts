import { describe, expect, it } from 'vitest'
import { Permissions } from '@/lib/permissions'

describe('Permissions', () => {
  it('exposes the backend permission codes', () => {
    expect(Permissions).toEqual({
      OrganizationManage: 'organizations.manage',
      OrganizationMembersManage: 'organizations.members.manage',
      OrganizationRolesManage: 'organizations.roles.manage',
      FilesManage: 'files.manage',
      ApiKeysManage: 'apikeys.manage',
      AuditLogsView: 'auditlogs.view',
      SettingsManage: 'settings.manage',
    })
  })

  it('has no duplicate codes', () => {
    const codes = Object.values(Permissions)

    expect(new Set(codes).size).toBe(codes.length)
  })
})
