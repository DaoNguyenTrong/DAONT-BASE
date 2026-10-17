import { createMemoryHistory, createRouter } from 'vue-router'
import { createTestingPinia } from '@pinia/testing'
import { flushPromises, mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import PageTitle from '@/layouts/PageTitle.vue'
import en from '@/locales/en'

const stub = { template: '<div />' }

async function mountTitle(initialPath: string) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: '/',
        name: 'layout',
        component: stub,
        children: [
          { path: '', name: 'home', component: stub, meta: { breadcrumbKey: 'nav.home' } },
          {
            path: 'organizations',
            name: 'organizations',
            component: stub,
            meta: { breadcrumbKey: 'nav.organizations' },
            children: [
              {
                path: 'roles',
                name: 'roles',
                component: stub,
                meta: { breadcrumbKey: 'nav.userGuide' },
              },
            ],
          },
        ],
      },
    ],
  })
  await router.push(initialPath)
  await router.isReady()
  const pinia = createTestingPinia({ createSpy: vi.fn, stubActions: false })
  const wrapper = mount(PageTitle, { global: { plugins: [pinia, router] } })
  return { wrapper, router }
}

describe('PageTitle', () => {
  it('shows the deepest crumb as the page title and earlier crumbs as links', async () => {
    const { wrapper } = await mountTitle('/organizations/roles')

    expect(wrapper.get('[data-testid="app-header-title"]').text()).toBe(en.nav.userGuide)
    const items = wrapper.findAll('.n-breadcrumb-item')
    expect(items).toHaveLength(2)
    expect(items[1]!.text()).toContain(en.nav.organizations)
    expect(items[1]!.find('a').attributes('href')).toBe('/organizations')
  })

  it('shows only the title on home', async () => {
    const { wrapper } = await mountTitle('/')

    expect(wrapper.get('[data-testid="app-header-title"]').text()).toBe(en.nav.home)
    expect(wrapper.find('.n-breadcrumb').exists()).toBe(false)
  })

  it('leads the trail with a home link', async () => {
    const { wrapper, router } = await mountTitle('/organizations')

    expect(wrapper.findAll('.n-breadcrumb-item')).toHaveLength(1)
    await wrapper.get('.n-breadcrumb-item a').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.name).toBe('home')
  })

  // The crumb is cut with an ellipsis when it is long, so its full text must stay reachable on hover.
  it('keeps the full crumb label in a title, for when a long one is truncated', async () => {
    const { wrapper } = await mountTitle('/organizations/roles')

    const crumb = wrapper.get('[data-testid="app-header-crumb-text"]')
    expect(crumb.attributes('title')).toBe(en.nav.organizations)
  })
})
