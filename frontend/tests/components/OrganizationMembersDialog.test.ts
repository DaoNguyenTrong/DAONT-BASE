import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import OrganizationMembersDialog from '@/components/OrganizationMembersDialog.vue'
import type { OrganizationDto, OrganizationMemberDto, RoleDto } from '@/api/types'
import { server } from '../helpers/msw/server'
import { renderComponent } from '../helpers/render'

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

function makeMember(overrides: Partial<OrganizationMemberDto> = {}): OrganizationMemberDto {
  return {
    accountId: 'acc-1',
    accountName: 'Bob',
    email: 'bob@example.com',
    roleIds: ['role-owner'],
    roleNames: ['Owner'],
    ...overrides,
  }
}

function mockOrgEndpoints(
  organizationId: string,
  members: OrganizationMemberDto[],
  roles: RoleDto[],
) {
  server.use(
    http.get(`*/api/organizations/${organizationId}/members`, () => HttpResponse.json(members)),
    http.get(`*/api/organizations/${organizationId}/roles`, () => HttpResponse.json(roles)),
  )
}

async function openDialog(organization: OrganizationDto) {
  const result = await renderComponent(OrganizationMembersDialog, {
    props: { organization, visible: false },
    global: { stubs: { teleport: true } },
  })
  await result.wrapper.setProps({ visible: true })
  await flushPromises()
  return result
}

describe('OrganizationMembersDialog', () => {
  it('loads and renders members', async () => {
    const organization = makeOrganization()
    mockOrgEndpoints(organization.id, [makeMember()], [makeRole()])

    const { wrapper } = await openDialog(organization)

    expect(wrapper.text()).toContain('Bob')
    expect(wrapper.text()).toContain('bob@example.com')
  })

  it('hides the add-member form when the caller lacks members-manage permission', async () => {
    const organization = makeOrganization({ myPermissionCodes: [] })
    mockOrgEndpoints(organization.id, [makeMember()], [makeRole()])

    const { wrapper } = await openDialog(organization)

    expect(wrapper.find('form').exists()).toBe(false)
  })

  it('shows the add-member form when the caller has members-manage permission', async () => {
    const organization = makeOrganization({
      myPermissionCodes: ['organizations.members.manage'],
    })
    mockOrgEndpoints(organization.id, [], [makeRole()])

    const { wrapper } = await openDialog(organization)

    expect(wrapper.find('form').exists()).toBe(true)
  })

  it('only shows the deactivate button to a caller with organization-manage permission', async () => {
    const organization = makeOrganization({
      myPermissionCodes: ['organizations.members.manage'],
    })
    mockOrgEndpoints(organization.id, [], [makeRole()])

    const { wrapper } = await openDialog(organization)

    expect(wrapper.text()).not.toContain('Deactivate organization')
  })

  it('reassigns a member roles and shows the updated selection', async () => {
    const organization = makeOrganization()
    const member = makeMember()
    const adminRole = makeRole({ id: 'role-admin', name: 'Admin' })
    let patchedBody: { roleIds: string[] } | null = null
    mockOrgEndpoints(organization.id, [member], [makeRole(), adminRole])
    server.use(
      http.patch(
        `*/api/organizations/${organization.id}/members/${member.accountId}`,
        async ({ request }) => {
          patchedBody = (await request.json()) as { roleIds: string[] }
          return new HttpResponse(null, { status: 204 })
        },
      ),
    )

    const { wrapper } = await openDialog(organization)

    const select = wrapper.findComponent({ name: 'Select' })
    expect(select.exists()).toBe(true)
    await select.vm.$emit('update:value', [member.roleIds[0], adminRole.id])
    await flushPromises()

    expect(patchedBody).toEqual({ roleIds: [member.roleIds[0], adminRole.id] })
  })

  it('adds a member with the selected roles and reloads the list', async () => {
    const organization = makeOrganization()
    let addedBody: { email: string; roleIds: string[] } | null = null
    let listCallCount = 0
    server.use(
      http.get(`*/api/organizations/${organization.id}/members`, () => {
        listCallCount++
        return HttpResponse.json([])
      }),
      http.get(`*/api/organizations/${organization.id}/roles`, () =>
        HttpResponse.json([makeRole()]),
      ),
      http.post(`*/api/organizations/${organization.id}/members`, async ({ request }) => {
        addedBody = (await request.json()) as { email: string; roleIds: string[] }
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { wrapper } = await openDialog(organization)
    expect(listCallCount).toBe(1)

    await wrapper.find('input[type="email"]').setValue('new@example.com')
    const select = wrapper.findComponent({ name: 'Select' })
    await select.vm.$emit('update:value', ['role-owner'])
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(addedBody).toEqual({ email: 'new@example.com', roleIds: ['role-owner'] })
    expect(listCallCount).toBe(2)
  })
})
