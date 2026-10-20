import { mount, type ComponentMountingOptions } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createRouter, createMemoryHistory, type Router } from 'vue-router'
import type { Component } from 'vue'
import { vi } from 'vitest'

type RenderComponentOptions<C extends Component> = ComponentMountingOptions<C> & {
  /** Seeds Pinia store state before mount — required for state read once at setup() time. */
  initialState?: Record<string, unknown>
  /** Initial history entry (path + query) — needed when the component reads route.query at mount time. */
  initialRoute?: string
}

export async function renderComponent<C extends Component>(
  component: C,
  options: RenderComponentOptions<C> = {},
) {
  const { initialState, initialRoute, ...mountingOptions } = options

  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: '/',
        name: 'home',
        component: { template: '<div />' },
        meta: { requiresAuth: true },
      },
      {
        path: '/organizations',
        name: 'organizations',
        component: { template: '<div />' },
        meta: { requiresAuth: true },
      },
      {
        path: '/login',
        name: 'login',
        component: { template: '<div />' },
        meta: { guestOnly: true },
      },
      {
        path: '/register',
        name: 'register',
        component: { template: '<div />' },
        meta: { guestOnly: true },
      },
      { path: '/verify-email', name: 'verify-email', component: { template: '<div />' } },
      {
        path: '/guide-auth',
        name: 'guide-auth',
        component: { template: '<div />' },
        meta: { requiresAuth: true },
      },
    ],
  })

  // Resolve the initial navigation BEFORE mounting: otherwise router.isReady() waits for mount()
  // to install the router, and a component reading route.query in onMounted sees no route yet.
  await router.push(initialRoute ?? '/')
  await router.isReady()

  const pinia = createTestingPinia({ createSpy: vi.fn, stubActions: false, initialState })

  const wrapper = mount(component, {
    ...mountingOptions,
    global: {
      ...mountingOptions.global,
      plugins: [pinia, router, ...(mountingOptions.global?.plugins ?? [])],
    },
  })

  await router.isReady()

  return { wrapper, router, pinia }
}
