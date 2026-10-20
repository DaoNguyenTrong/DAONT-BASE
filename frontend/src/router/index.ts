import type { Component } from 'vue'
import { createRouter, createWebHashHistory } from 'vue-router'
import { Building, Home } from '@vicons/tabler'

declare module 'vue-router' {
  interface RouteMeta {
    requiresAuth?: boolean
    guestOnly?: boolean
    /**
     * Permission code (`lib/permissions.ts`) the active organization must grant: hides the route's
     * sidebar item and sends a signed-in user without it to home. A UI gate only — the API still
     * checks every request. Never put one on `home`, the fallback itself.
     */
    permission?: string
    breadcrumbKey?: string
    sidebar?: {
      labelKey: string
      icon: Component
      order?: number
      /** `MenuGroup.id` from `layouts/menu-groups.ts`; omit for a standalone top-level item. */
      group?: string
    }
  }
}

const router = createRouter({
  history: createWebHashHistory(),
  // Only reaches pages that scroll the window (login, register…), where back/forward restores the
  // saved position. Inside the app shell the page scrolls `main` instead, which AppLayout resets to
  // the top on every path change — back/forward included; no position is saved for it.
  scrollBehavior: (_to, _from, savedPosition) => savedPosition ?? { top: 0 },
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
      // nginx sends a reader of the private user guide here when their session cookie is missing or
      // expired; the guard routes them through login, then the view sends them back to the guide.
      path: '/guide-auth',
      name: 'guide-auth',
      component: () => import('@/views/GuideAuthView.vue'),
      meta: { requiresAuth: true },
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
    meta: { requiresAuth?: boolean; guestOnly?: boolean; permission?: string }
    fullPath: string
    query: Record<string, unknown>
  },
  auth: { isAuthenticated: boolean; hasPermission: (code: string) => boolean },
  health: { isDown: boolean },
) {
  // During an outage the silent refresh couldn't run, so `isAuthenticated` is unreliable: don't
  // redirect (no /login flash); keep the URL so App.vue's recovery watcher can restore and route.
  if (health.isDown) {
    return undefined
  }

  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    // Preserve the destination so LoginView can return the user there after
    // signing in, instead of always dropping them on the home page.
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  // Hiding the sidebar item isn't enough: a typed URL or a bookmark still reaches the route.
  if (to.meta.permission && auth.isAuthenticated && !auth.hasPermission(to.meta.permission)) {
    return useHomeRoute()
  }

  if (to.meta.guestOnly && auth.isAuthenticated) {
    return safeRedirectTarget(to.query.redirect) ?? useHomeRoute()
  }

  return undefined
}

router.beforeEach((to) => resolveGuardRedirect(to, useAuthStore(), useHealthStore()))

export default router
