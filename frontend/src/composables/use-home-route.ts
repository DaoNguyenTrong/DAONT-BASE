import type { RouteLocationRaw } from 'vue-router'

export function useHomeRoute(): RouteLocationRaw {
  return { name: 'home' }
}
