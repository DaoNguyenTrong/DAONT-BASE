import type { Component } from 'vue'
import { createRouter, createWebHashHistory } from 'vue-router'
import { Building, Home } from '@vicons/tabler'

declare module 'vue-router' {
  interface RouteMeta {
    requiresAuth?: boolean
    guestOnly?: boolean
    breadcrumbKey?: string
    sidebar?: {
      labelKey: string
      icon: Component
      order?: number
    }
  }
}

const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    {
      path: '/',
      component: () => import('@/layouts/AppLayout.vue'),
      children: [
        {
          path: '',
          name: 'home',
          component: () => import('@/views/HomeView.vue'),
          meta: {
            requiresAuth: true,
            breadcrumbKey: 'nav.home',
            sidebar: {
              labelKey: 'nav.home',
              icon: Home,
            },
          },
        },
        {
          path: 'organizations',
          name: 'organizations',
          component: () => import('@/views/OrganizationsView.vue'),
          meta: {
            requiresAuth: true,
            breadcrumbKey: 'nav.organizations',
            sidebar: {
              labelKey: 'nav.organizations',
              icon: Building,
            },
          },
        },
      ],
    },
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
      meta: { guestOnly: true },
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/views/RegisterView.vue'),
      meta: { guestOnly: true },
    },
    {
      path: '/verify-email',
      name: 'verify-email',
      component: () => import('@/views/VerifyEmailView.vue'),
    },
    {
      path: '/:pathMatch(.*)*',
      name: 'not-found',
      component: () => import('@/views/NotFoundView.vue'),
    },
  ],
})

export function resolveGuardRedirect(
  to: {
    meta: { requiresAuth?: boolean; guestOnly?: boolean }
    fullPath: string
    query: Record<string, unknown>
  },
  auth: { isAuthenticated: boolean },
  health: { isDown: boolean },
) {
  // During an API outage, ServerErrorScreen takes over the whole screen and the
  // silent refresh could not run — so `auth.isAuthenticated` is unreliable.
  // Don't redirect anywhere (no /login flash): keep the URL on the intended
  // route so App.vue's recovery watcher can restore the session and route once
  // the API comes back.
  if (health.isDown) {
    return undefined
  }

  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    // Preserve the destination so LoginView can return the user there after
    // signing in, instead of always dropping them on the home page.
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  if (to.meta.guestOnly && auth.isAuthenticated) {
    return safeRedirectTarget(to.query.redirect) ?? useHomeRoute()
  }

  return undefined
}

router.beforeEach((to) => resolveGuardRedirect(to, useAuthStore(), useHealthStore()))

export default router
