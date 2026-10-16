import { defineComponent } from 'vue'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { useOrganizationSwitch } from '@/composables/use-organization-switch'
import { reloadTo } from '@/lib/session-exit'
import { http, HttpResponse } from 'msw'
import { useAuthStore } from '@/stores/auth'
import { makeAccount, makeAuthResponse } from '../../helpers/msw/fixtures'
import { server } from '../../helpers/msw/server'
import { renderComponent } from '../../helpers/render'
import { storeIn } from '../../helpers/pinia'

vi.mock('@/lib/session-exit', () => ({
  reloadTo: vi.fn(),
  reloadToLogin: vi.fn(),
}))

async function setup() {
  let api!: ReturnType<typeof useOrganizationSwitch>
  const Host = defineComponent({
    setup() {
      api = useOrganizationSwitch()
      return () => null
    },
  })
  const { wrapper, router, pinia } = await renderComponent(Host, { initialRoute: '/organizations' })
  return { api, wrapper, router, auth: storeIn(pinia, useAuthStore) }
}

describe('useOrganizationSwitch', () => {
  beforeEach(() => {
    vi.mocked(reloadTo).mockClear()
  })

  it('switches the organization, then reloads to the home route', async () => {
    let body: unknown
    server.use(
      http.post('*/api/auth/switch-organization', async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ ...makeAuthResponse(), organizationId: 'org-2' })
      }),
    )
    const { api, wrapper, auth } = await setup()
    auth.account = makeAccount()
    auth.organizationId = 'org-1'

    await api.switchOrganization('org-2')

    expect(body).toEqual({ organizationId: 'org-2' })
    // Not committed to the store: the page must not re-render for the new tenant before the reload.
    expect(auth.organizationId).toBe('org-1')
    // A full reload, so no store keeps the previous organization's data — and back to home,
    // since the current page may belong to the organization just left.
    expect(reloadTo).toHaveBeenCalledWith('/')
    wrapper.unmount()
  })

  it('does not reload when the switch fails', async () => {
    server.use(
      http.post('*/api/auth/switch-organization', () => HttpResponse.json({}, { status: 403 })),
    )
    const { api, wrapper } = await setup()

    await expect(api.switchOrganization(null)).rejects.toBeDefined()

    expect(reloadTo).not.toHaveBeenCalled()
    wrapper.unmount()
  })
})
