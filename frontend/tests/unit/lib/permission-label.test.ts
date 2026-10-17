import { describe, expect, it, vi } from 'vitest'
import i18n from '@/plugins/i18n'
import { permissionLabel } from '@/lib/permission-label'
import { Permissions } from '@/lib/permissions'

const translate = (key: string) => i18n.global.t(key)

describe('permissionLabel', () => {
  it('looks up the translation key mapped to a known permission', () => {
    const t = vi.fn((key: string) => `translated:${key}`)

    expect(permissionLabel(Permissions.OrganizationManage, t)).toBe(
      'translated:organizations.permissionOrganizationManage',
    )
    expect(permissionLabel(Permissions.OrganizationMembersManage, t)).toBe(
      'translated:organizations.permissionOrganizationMembersManage',
    )
    expect(permissionLabel(Permissions.OrganizationRolesManage, t)).toBe(
      'translated:organizations.permissionOrganizationRolesManage',
    )
  })

  it('returns the raw code for an unknown permission without calling t', () => {
    const t = vi.fn((key: string) => key)

    expect(permissionLabel('billing.manage', t)).toBe('billing.manage')
    expect(permissionLabel('', t)).toBe('')
    expect(t).not.toHaveBeenCalled()
  })

  it.each(['en', 'vi'] as const)(
    'resolves every known permission to a translated label in %s',
    (locale) => {
      i18n.global.locale.value = locale

      for (const code of Object.values(Permissions)) {
        const label = permissionLabel(code, translate)

        expect(label).not.toBe(code)
        expect(label).not.toMatch(/^organizations\.permission/)
        expect(label.trim()).not.toBe('')
      }
    },
  )
})
