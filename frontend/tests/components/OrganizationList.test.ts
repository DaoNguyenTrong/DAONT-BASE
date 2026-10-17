import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import i18n from '@/plugins/i18n'
import OrganizationList from '@/components/OrganizationList.vue'
import { reloadTo } from '@/lib/session-exit'
import { makeAuthResponse } from '../helpers/msw/fixtures'
import { server } from '../helpers/msw/server'
import { makeOrganization } from '../factories/organization'
import { renderComponent } from '../helpers/render'

vi.mock('@/lib/session-exit', () => ({
  reloadTo: vi.fn(),
  reloadToLogin: vi.fn(),
}))

const organizations = [
  makeOrganization({ id: 'org-1', name: 'Acme', slug: 'acme' }),
  makeOrganization({ id: 'org-2', name: 'Globex', slug: 'globex' }),
]

function render(auth: Record<string, unknown> = { organizationId: 'org-1' }) {
  return renderComponent(OrganizationList, {
    initialState: {
      auth,
      organizations: { loaded: true, myOrganizations: organizations },
    },
  })
}

describe('OrganizationList', () => {
  beforeEach(() => {
    vi.mocked(reloadTo).mockClear()
  })

  it('lists the personal workspace and every organization', async () => {
    const { wrapper } = await render()

    expect(wrapper.get('[data-testid="organization-list"]').text()).toContain(
      i18n.global.t('organizations.personalWorkspace'),
    )
    expect(wrapper.text()).toContain('Acme')
    expect(wrapper.text()).toContain('Globex')
  })

  it('loads the organizations on mount when they are not loaded yet', async () => {
    server.use(http.get('*/api/organizations', () => HttpResponse.json(organizations)))
    const { wrapper } = await renderComponent(OrganizationList, {
      initialState: { auth: { organizationId: null } },
    })
    await flushPromises()

    expect(wrapper.text()).toContain('Globex')
  })

  it('switches organization through the API and reloads', async () => {
    let body: unknown
    server.use(
      http.post('*/api/auth/switch-organization', async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ ...makeAuthResponse(), organizationId: 'org-2' })
      }),
    )
    const { wrapper } = await render()

    const globex = wrapper.findAll('button').find((button) => button.text() === 'Globex')!
    await globex.trigger('click')
    await flushPromises()

    expect(body).toEqual({ organizationId: 'org-2' })
    expect(reloadTo).toHaveBeenCalledWith('/')
  })

  it('emits done when the current organization is picked', async () => {
    const { wrapper } = await render()

    const acme = wrapper.findAll('button').find((button) => button.text() === 'Acme')!
    await acme.trigger('click')

    expect(wrapper.emitted('done')).toHaveLength(1)
    expect(reloadTo).not.toHaveBeenCalled()
  })

  it('goes to organizations on manage', async () => {
    const { wrapper, router } = await render()

    await wrapper.get('[data-testid="organization-list-manage"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('organizations')
    expect(wrapper.emitted('done')).toHaveLength(1)
  })
})
