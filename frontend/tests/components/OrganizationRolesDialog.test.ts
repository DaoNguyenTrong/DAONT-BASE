import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import OrganizationRolesDialog from '@/components/OrganizationRolesDialog.vue'
import type { OrganizationDto, PermissionDto, RoleDto } from '@/api/types'
import { server } from '../helpers/msw/server'
import { renderComponent } from '../helpers/render'

const mockOpen = vi.hoisted(() => vi.fn())

vi.mock('@/composables/use-app-dialog-naive', () => ({
  useAppDialogNaive: () => ({ open: mockOpen }),
}))

function makeOrganization(overrides: Partial<OrganizationDto> = {}): OrganizationDto {
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

function makeRole(overrides: Partial<RoleDto> = {}): RoleDto {
  return { id: 'role-owner', name: 'Owner', isSystem: true, permissionCodes: [], ...overrides }
}

function mockRolesEndpoints(
  organizationId: string,
  roles: RoleDto[],
  permissions: PermissionDto[] = [],
) {
  server.use(
    http.get(`*/api/organizations/${organizationId}/roles`, () => HttpResponse.json(roles)),
    http.get('*/api/permissions', () => HttpResponse.json(permissions)),
  )
}

async function openDialog(organization: OrganizationDto) {
  const result = await renderComponent(OrganizationRolesDialog, {
    props: { organization, visible: false },
    global: { stubs: { teleport: true } },
  })
  await result.wrapper.setProps({ visible: true })
  await flushPromises()
  return result
}

describe('OrganizationRolesDialog', () => {
  beforeEach(() => {
    mockOpen.mockReset()
  })

  it('renders roles with a system badge and no edit/delete buttons for system roles', async () => {
    const organization = makeOrganization()
    mockRolesEndpoints(organization.id, [makeRole()])

    const { wrapper } = await openDialog(organization)

    expect(wrapper.text()).toContain('Owner')
    expect(wrapper.text()).toContain('System')
    expect(wrapper.findAll('button[aria-label="Edit role"]')).toHaveLength(0)
  })

  it('shows edit/delete buttons for a custom role when the caller can manage roles', async () => {
    const organization = makeOrganization()
    const customRole = makeRole({ id: 'role-custom', name: 'Billing Manager', isSystem: false })
    mockRolesEndpoints(organization.id, [customRole])

    const { wrapper } = await openDialog(organization)

    expect(wrapper.find('button[aria-label="Edit role"]').exists()).toBe(true)
    expect(
      wrapper
        .find(
          'button[aria-label="Delete this role? Members holding only this role will lose its permissions."]',
        )
        .exists(),
    ).toBe(true)
  })

  it('hides create/edit/delete affordances when the caller lacks roles-manage permission', async () => {
    const organization = makeOrganization({
      myPermissionCodes: ['organizations.members.manage'],
    })
    const customRole = makeRole({ id: 'role-custom', name: 'Billing Manager', isSystem: false })
    mockRolesEndpoints(organization.id, [customRole])

    const { wrapper } = await openDialog(organization)

    expect(wrapper.find('button[aria-label="Edit role"]').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('Create role')
  })

  it('opens the create-role dialog and creates a role on confirm', async () => {
    const organization = makeOrganization()
    let createBody: { name: string; permissionCodes: string[] } | null = null
    mockRolesEndpoints(organization.id, [], [{ code: 'organizations.members.manage' }])
    server.use(
      http.post(`*/api/organizations/${organization.id}/roles`, async ({ request }) => {
        createBody = (await request.json()) as { name: string; permissionCodes: string[] }
        return HttpResponse.json(
          makeRole({
            id: 'role-new',
            name: createBody.name,
            isSystem: false,
            permissionCodes: createBody.permissionCodes,
          }),
          { status: 201 },
        )
      }),
    )

    const { wrapper } = await openDialog(organization)

    const createButton = wrapper.findAll('button').find((b) => b.text().includes('Create role'))
    expect(createButton).toBeDefined()
    await createButton!.trigger('click')
    expect(mockOpen).toHaveBeenCalledTimes(1)
    const [, options] = mockOpen.mock.calls[0]!
    options.data.state.name = 'Billing Manager'
    options.data.state.permissionCodes.push('organizations.members.manage')

    const close = vi.fn()
    await options.onConfirm(close)

    expect(createBody).toEqual({
      name: 'Billing Manager',
      permissionCodes: ['organizations.members.manage'],
    })
    expect(close).toHaveBeenCalledTimes(1)
  })
})
