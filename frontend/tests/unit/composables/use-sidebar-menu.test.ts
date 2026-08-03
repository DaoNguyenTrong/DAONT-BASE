import type { RouteRecordRaw } from 'vue-router'
import { describe, expect, it } from 'vitest'
import { useSidebarMenu } from '@/composables/use-sidebar-menu'
import { withSetup } from '../../helpers/with-setup'

const stub = { template: '<div />' }

function layoutRoutes(children: RouteRecordRaw[]): RouteRecordRaw[] {
  return [{ path: '/', name: 'layout', component: stub, children }]
}

describe('useSidebarMenu', () => {
  it('excludes routes without meta.sidebar', async () => {
    const routes = layoutRoutes([
      {
        path: 'dashboard',
        name: 'dashboard',
        component: stub,
        meta: { sidebar: { labelKey: 'nav.dashboard', icon: stub, order: 0 } },
      },
      { path: 'hidden', name: 'hidden', component: stub, meta: {} },
      { path: 'chat', name: 'chat', component: stub },
    ])

    const { result } = await withSetup(() => useSidebarMenu(), '/', routes)

    expect(result.value.map((item) => item.routeName)).toEqual(['dashboard'])
  })

  it('orders siblings by meta.sidebar.order ascending, defaulting missing order to 0', async () => {
    const routes = layoutRoutes([
      {
        path: 'third',
        name: 'third',
        component: stub,
        meta: { sidebar: { labelKey: 'nav.third', icon: stub, order: 5 } },
      },
      {
        path: 'first',
        name: 'first',
        component: stub,
        meta: { sidebar: { labelKey: 'nav.first', icon: stub, order: -1 } },
      },
      {
        path: 'second',
        name: 'second',
        component: stub,
        meta: { sidebar: { labelKey: 'nav.second', icon: stub } },
      },
    ])

    const { result } = await withSetup(() => useSidebarMenu(), '/', routes)

    expect(result.value.map((item) => item.routeName)).toEqual(['first', 'second', 'third'])
  })

  it('builds a submenu item for a route whose children have meta.sidebar', async () => {
    const routes = layoutRoutes([
      {
        path: 'settings',
        name: 'settings',
        component: stub,
        meta: { sidebar: { labelKey: 'nav.settings', icon: stub, order: 0 } },
        children: [
          {
            path: 'profile',
            name: 'settings-profile',
            component: stub,
            meta: { sidebar: { labelKey: 'nav.profile', icon: stub, order: 0 } },
          },
          {
            path: 'security',
            name: 'settings-security',
            component: stub,
            meta: { sidebar: { labelKey: 'nav.security', icon: stub, order: 1 } },
          },
        ],
      },
    ])

    const { result } = await withSetup(() => useSidebarMenu(), '/', routes)

    expect(result.value).toHaveLength(1)
    const submenu = result.value[0]!
    expect(submenu.routeName).toBeUndefined()
    expect(submenu.labelKey).toBe('nav.settings')
    expect(submenu.items?.map((item) => item.routeName)).toEqual([
      'settings-profile',
      'settings-security',
    ])
  })

  it('excludes a redirect route with no qualifying children even when it has meta.sidebar', async () => {
    const routes = layoutRoutes([
      {
        path: 'legacy',
        name: 'legacy',
        redirect: '/dashboard',
        meta: { sidebar: { labelKey: 'nav.legacy', icon: stub, order: 0 } },
      },
      {
        path: 'dashboard',
        name: 'dashboard',
        component: stub,
        meta: { sidebar: { labelKey: 'nav.dashboard', icon: stub, order: 1 } },
      },
    ])

    const { result } = await withSetup(() => useSidebarMenu(), '/', routes)

    expect(result.value.map((item) => item.routeName)).toEqual(['dashboard'])
  })
})
