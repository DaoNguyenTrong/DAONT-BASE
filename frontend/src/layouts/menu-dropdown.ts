import { h } from 'vue'
import { NIcon, type DropdownOption } from 'naive-ui'
import type { SidebarMenuItem } from './sidebar-menu'

/**
 * Naive UI dropdown options for menu items (rail flyouts, the horizontal bar). An item with children
 * becomes a submenu; a leaf's key is its route name, so a selected key is the route to open.
 */
export function menuDropdownOptions(
  items: readonly SidebarMenuItem[],
  t: (key: string) => string,
): DropdownOption[] {
  return items.map((item) => ({
    label: t(item.labelKey),
    key: item.routeName ?? item.labelKey,
    icon: () => h(NIcon, null, { default: () => h(item.icon) }),
    children: item.items?.length ? menuDropdownOptions(item.items, t) : undefined,
  }))
}
