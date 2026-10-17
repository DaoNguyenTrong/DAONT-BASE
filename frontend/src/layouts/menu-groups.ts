import type { Component } from 'vue'

export interface MenuGroup {
  id: string
  labelKey: string
  icon: Component
  order: number
}

/**
 * Sidebar menu groups. Forks add their groups here, then assign a route with
 * `meta.sidebar.group: '<id>'`. Base ships none, so every item stays a standalone top-level entry.
 */
export const MENU_GROUPS: MenuGroup[] = []
