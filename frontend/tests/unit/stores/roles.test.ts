import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach } from 'vitest'
import { useRolesStore } from '@/stores/roles'
import type { PermissionDto, RoleDto } from '@/api/types'
import { setupTestPinia } from '../../helpers/pinia'
import { server } from '../../helpers/msw/server'

const ORG_ID = 'org-1'

function makeRole(overrides: Partial<RoleDto> = {}): RoleDto {
  return {
    id: 'role-1',
    name: 'Owner',
    isSystem: true,
    permissionCodes: [
      'organizations.manage',
      'organizations.members.manage',
      'organizations.roles.manage',
    ],
    ...overrides,
  }
}

function makePermission(code: string): PermissionDto {
  return { code }
}

describe('useRolesStore', () => {
  beforeEach(() => {
    setupTestPinia()
  })

  it('fetchRoles loads the roles for an organization', async () => {
    server.use(
      http.get(`*/api/organizations/${ORG_ID}/roles`, () =>
        HttpResponse.json([
          makeRole(),
          makeRole({ id: 'role-2', name: 'Member', isSystem: true, permissionCodes: [] }),
        ]),
      ),
    )
    const roles = useRolesStore()

    const result = await roles.fetchRoles(ORG_ID)

    expect(result).toHaveLength(2)
    expect(roles.roles.map((r) => r.name)).toEqual(['Owner', 'Member'])
  })

  it('fetchPermissionCatalog loads the catalog once and caches it', async () => {
    let calls = 0
    server.use(
      http.get('*/api/permissions', () => {
        calls++
        return HttpResponse.json([
          makePermission('organizations.manage'),
          makePermission('organizations.members.manage'),
        ])
      }),
    )
    const roles = useRolesStore()

    await roles.fetchPermissionCatalog()
    await roles.fetchPermissionCatalog()

    expect(calls).toBe(1)
    expect(roles.permissionCatalog).toHaveLength(2)
  })

  it('create adds the new role to state', async () => {
    server.use(
      http.post(`*/api/organizations/${ORG_ID}/roles`, async ({ request }) => {
        const body = (await request.json()) as { name: string; permissionCodes: string[] }
        return HttpResponse.json(
          makeRole({
            id: 'role-new',
            name: body.name,
            isSystem: false,
            permissionCodes: body.permissionCodes,
          }),
          { status: 201 },
        )
      }),
    )
    const roles = useRolesStore()

    const created = await roles.create(ORG_ID, {
      name: 'Billing Manager',
      permissionCodes: ['organizations.members.manage'],
    })

    expect(created.name).toBe('Billing Manager')
    expect(roles.roles).toContainEqual(created)
  })

  it('update replaces the role in state', async () => {
    server.use(
      http.patch(`*/api/organizations/${ORG_ID}/roles/role-new`, async ({ request }) => {
        const body = (await request.json()) as { name: string; permissionCodes: string[] }
        return HttpResponse.json(
          makeRole({
            id: 'role-new',
            name: body.name,
            isSystem: false,
            permissionCodes: body.permissionCodes,
          }),
        )
      }),
    )
    const roles = useRolesStore()
    roles.roles = [
      makeRole({ id: 'role-new', name: 'Old Name', isSystem: false, permissionCodes: [] }),
    ]

    const updated = await roles.update(ORG_ID, 'role-new', {
      name: 'New Name',
      permissionCodes: ['organizations.manage'],
    })

    expect(updated.name).toBe('New Name')
    expect(roles.roles).toEqual([updated])
  })

  it('remove drops the role from state', async () => {
    server.use(
      http.delete(
        `*/api/organizations/${ORG_ID}/roles/role-new`,
        () => new HttpResponse(null, { status: 204 }),
      ),
    )
    const roles = useRolesStore()
    roles.roles = [makeRole({ id: 'role-new', isSystem: false })]

    await roles.remove(ORG_ID, 'role-new')

    expect(roles.roles).toEqual([])
  })
})
