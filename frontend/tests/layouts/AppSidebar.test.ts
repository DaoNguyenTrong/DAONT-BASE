import { createMemoryHistory, createRouter } from 'vue-router'
import { createTestingPinia } from '@pinia/testing'
import { flushPromises, mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import { Building, Home } from '@vicons/tabler'
import AppSidebar from '@/layouts/AppSidebar.vue'
import ProfileDialog from '@/components/ProfileDialog.vue'
import { useAuthStore } from '@/stores/auth'
import { useSidebarStore } from '@/stores/sidebar-store'

const stub = { template: '<div />' }

async function buildRouter(initialPath = '/') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: '/',
        name: 'layout',
        component: stub,
        children: [
          {
            path: '',
            name: 'home',
            component: stub,
            meta: { sidebar: { labelKey: 'nav.home', icon: Home } },
          },
          {
            path: 'organizations',
            name: 'organizations',
            component: stub,
            meta: { sidebar: { labelKey: 'nav.organizations', icon: Building } },
          },
        ],
      },
      { path: '/login', name: 'login', component: stub },
    ],
  })
  await router.push(initialPath)
  await router.isReady()
  return router
}

async function mountSidebar(initialState: Record<string, unknown> = {}) {
  const router = await buildRouter()
  const pinia = createTestingPinia({ createSpy: vi.fn, stubActions: false, initialState })

  const wrapper = mount(AppSidebar, {
    global: {
      plugins: [pinia, router],
      stubs: { teleport: true, ProfileDialog: true },
    },
  })

  return { wrapper, router }
}

describe('AppSidebar', () => {
  it('renders one sidebar item per route with sidebar meta', async () => {
    const { wrapper } = await mountSidebar()

    const links = wrapper.findAll('nav a')
    expect(links.map((l) => l.text())).toEqual(['Home', 'Organizations'])
  })

  it('shows the logged-in account name', async () => {
    const { wrapper } = await mountSidebar({
      auth: { account: { id: '1', name: 'Jane Doe', username: 'jane' } },
    })

    expect(wrapper.text()).toContain('Jane Doe')
  })

  it('falls back to username, then "User", when the account has no name', async () => {
    const { wrapper: withUsername } = await mountSidebar({
      auth: { account: { id: '1', name: null, username: 'jane' } },
    })
    expect(withUsername.text()).toContain('jane')

    const { wrapper: withNeither } = await mountSidebar({
      auth: { account: { id: '1', name: null, username: null } },
    })
    expect(withNeither.text()).toContain('User')
  })

  it('opens the profile dialog and closes the mobile drawer when the profile button is clicked', async () => {
    const { wrapper } = await mountSidebar()
    const sidebar = useSidebarStore()
    sidebar.openMobile()

    await wrapper.get('button[aria-label="Profile"]').trigger('click')

    expect(sidebar.mobileOpen).toBe(false)
    expect(wrapper.getComponent(ProfileDialog).props('visible')).toBe(true)
  })

  it('navigates home and closes the mobile drawer when the logo button is clicked', async () => {
    const { wrapper, router } = await mountSidebar()
    await router.push('/organizations')
    const sidebar = useSidebarStore()
    sidebar.openMobile()

    await wrapper.get('button.h-16').trigger('click')
    await flushPromises()

    expect(sidebar.mobileOpen).toBe(false)
    expect(router.currentRoute.value.name).toBe('home')
  })

  it('logs out, closes the mobile drawer, and redirects to login on confirm', async () => {
    const { wrapper, router } = await mountSidebar()
    const auth = useAuthStore()
    const logoutSpy = vi.spyOn(auth, 'logout').mockResolvedValue(undefined)
    const sidebar = useSidebarStore()
    sidebar.openMobile()

    await wrapper.get('button[aria-label="Logout"]').trigger('click')
    await flushPromises()

    expect(logoutSpy).toHaveBeenCalled()
    expect(sidebar.mobileOpen).toBe(false)
    expect(router.currentRoute.value.name).toBe('login')

    logoutSpy.mockRestore()
  })
})
