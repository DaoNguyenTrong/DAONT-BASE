import type { Component } from 'vue'
import { createRouter, createWebHashHistory } from 'vue-router'
import { Building, Home, Users } from '@vicons/tabler'

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
          path: 'accounts',
          name: 'accounts',
          component: () => import('@/views/AccountsView.vue'),
          meta: {
            requiresAuth: true,
            breadcrumbKey: 'nav.accounts',
            sidebar: {
              labelKey: 'nav.accounts',
              icon: Users,
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
  to: { meta: { requiresAuth?: boolean; guestOnly?: boolean } },
  auth: { isAuthenticated: boolean },
) {
  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    return { name: 'login' }
  }

  if (to.meta.guestOnly && auth.isAuthenticated) {
    return useHomeRoute()
  }

  return undefined
}

router.beforeEach((to) => resolveGuardRedirect(to, useAuthStore()))

export default router
