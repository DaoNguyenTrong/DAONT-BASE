import type { Component } from 'vue'

export interface SidebarMenuItem {
  labelKey: string
  icon: Component
  routeName?: string
  items?: SidebarMenuItem[]
}
